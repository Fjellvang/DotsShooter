import os
import sys
import shlex
import shutil
import urllib.request
import asyncio
import argparse
import colorama
import traceback
import logging
import json
import xml.etree.ElementTree as ElementTree
from timeit import default_timer as get_elapsed_seconds
from typing import Tuple, List

class Color:
  HEADER = '\033[95m'
  OKBLUE = '\033[94m'
  OKCYAN = '\033[96m'
  OKGREEN = '\033[92m'
  WARNING = '\033[93m'
  FAIL = '\033[91m'
  ENDC = '\033[0m'
  BOLD = '\033[1m'
  UNDERLINE = '\033[4m'

def prefix_lines(text: str, prefix: str) -> str:
  lines = text.splitlines()
  prefixed = [f'{prefix}{line}' for line in lines]
  return '\n'.join(prefixed)

# A invocation task (eg, docker run) failed

class TaskExecutionError(Exception):
  def __init__(self, message, command, logLines):
    super().__init__(message)  # Initialize the base Exception with the message
    self.message = message
    self.command = command
    self.logs = '\n'.join(logLines)

  def __str__(self):
    # Customize the string representation of the exception
    return f"{self.__class__.__name__}(message={self.args[0]})"

# Async OS Process

class AsyncProcess:
  def __init__(self, log: logging.Logger, directory: str, command: str, pipe_stdin: bool = False, verbose: bool = True):
    self.log = log
    self.directory = directory
    self.command = command
    self.pipe_stdin = pipe_stdin
    self.verbose = verbose
    self.proc = None

  async def run(self):
    self.log.info(f'Run process: {self.command}')
    cmd = shlex.split(self.command)
    executable = shutil.which(cmd[0])
    try:
      self.proc = await asyncio.create_subprocess_exec(
        executable,
        *cmd[1:],
        stdin = asyncio.subprocess.PIPE if self.pipe_stdin else None,
        stdout = asyncio.subprocess.PIPE,
        stderr = asyncio.subprocess.PIPE,
        cwd = self.directory
      )
    except Exception as ex:
      self.log.error(f'{Color.FAIL}ERROR Failed to execute process "{self.command}": {ex}{Color.ENDC}')
      raise

    self.stdout_lines = []
    self.stderr_lines = []

    task_stdout = asyncio.create_task(self._stdout())
    task_stderr = asyncio.create_task(self._stderr())
    await asyncio.gather(task_stdout, task_stderr)

    self.returncode = await self.proc.wait()

  def get_output(self):
    stdout = '\n'.join(self.stdout_lines)
    stderr = '\n'.join(self.stderr_lines)
    return f'<<<\nDIR: {self.directory} COMMAND: {self.command}\n\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}\n>>>'.replace('\r', '')

  # reader for stdout (short name to keep logs a bit more contained)
  async def _stdout(self):
    while True:
      line = await self.proc.stdout.readline()
      if not line:
        break
      line = line.decode('utf-8').rstrip()
      if self.verbose:
        self.log.debug(f'  {line}')
      self.stdout_lines.append(line)

  # reader for stdout (short name to keep logs a bit more contained)
  async def _stderr(self):
    while True:
      line = await self.proc.stderr.readline()
      if not line:
        break
      line = line.decode('utf-8').rstrip()
      if self.verbose:
        self.log.debug(f'  {line}')
      self.stderr_lines.append(line)

async def run_process(log: logging.Logger, directory: str, command: str, pipe_stdin: bool = False, verbose = True):
  proc = AsyncProcess(log, directory, command, pipe_stdin, verbose)
  await proc.run()
  return proc

### Parse command line arguments

parser = argparse.ArgumentParser(description='Metaplay automated integration test runner')
parser.add_argument('--project-dir', type=str, default='.', help='Path to project directory, relative to working directory')
parser.add_argument('--backend-dir', type=str, default='Backend', help='Path to project backend directory, relative to --project-dir')
parser.add_argument('--results-dir', type=str, default='results', help='Base directory for outputs from the test run (eg, Playwright screenshots), relative to working directory')
parser.add_argument('--name-prefix', type=str, default='metatest', help='Prefix string to use for docker images and containers')
parser.add_argument('--use-buildkit', default=False, action='store_true', help='Use legacy docker BuildKit instead of the more modern buildx. At least Bitbucket seems to have spotty support for buildx.')
parser.add_argument('-q', '--quiet', default=False, action='store_true', help='Run quietly, don\'t log the outputs from the process invocations.')
parser.add_argument('tests', nargs='*', help='List of tests to run, default is to run all tests')
args = parser.parse_args()

# If using BuildKit, set environment variable DOCKER_BUILDKIT=1 globally
if args.use_buildkit:
  print('Using Docker BuildKit')
  os.environ['DOCKER_BUILDKIT'] = '1'

SERVER_IMAGE_NAME = f'{args.name_prefix}-server:test'
PLAYWRIGHT_TS_IMAGE_NAME = f'{args.name_prefix}-playwright-ts:test'
PLAYWRIGHT_NET_IMAGE_NAME = f'{args.name_prefix}-playwright-net:test'
SERVER_CONTAINER_NAME = f'{args.name_prefix}-server'
BOTCLIENT_CONTAINER_NAME = f'{args.name_prefix}-botclient'
DASHBOARD_CONTAINER_NAME = f'{args.name_prefix}-dashboard'
PLAYWRIGHT_TS_CONTAINER_NAME = f'{args.name_prefix}-playwright-ts'
PLAYWRIGHT_NET_CONTAINER_NAME = f'{args.name_prefix}-playwright'

METAPLAY_SDK_DIR = os.path.relpath(os.path.join(os.path.dirname(__file__), '..'), '.').replace('\\','/')
PROJECT_BACKEND_DIR = os.path.join(args.project_dir, args.backend_dir).replace('\\', '/')

args.backend_dir = args.backend_dir.replace('\\', '/')
args.project_dir = args.project_dir.replace('\\', '/')

if not os.path.exists(os.path.join(args.project_dir)):
  parser.error(f'Unable to find the project directory "{args.project_dir}", make sure your --project-dir is correct!')

if not os.path.exists(os.path.join(args.project_dir, args.backend_dir)):
  parser.error(f'Unable to find the project backend directory "{args.project_dir}/{args.backend_dir}", make sure your --project-dir and --backend-dir are correct!')

if not os.path.exists(os.path.join(PROJECT_BACKEND_DIR, 'Server')):
  parser.error(f'The project backend directory "{args.project_dir}/{args.backend_dir}" doesn\'t contain a "Server" subdirectory, make sure your --project-dir and --backend-dir are correct!')

METAPLAY_OPTS = ' '.join([
  '--Environment:EnableKeyboardInput=false',
  '--Environment:ExitOnLogError=true',
  ])
METAPLAY_SERVER_OPTS = ' '.join([
  '--Environment:EnableSystemHttpServer=true',
  '--Environment:SystemHttpListenHost=0.0.0.0',
  '--AdminApi:WebRootPath=wwwroot',
  '--Database:Backend=Sqlite',
  '--Database:SqliteInMemory=true',
  '--Player:ForceFullDebugConfigForBots=false'
  ])

# Try to resolve dotnet version from the project's Backend/global.json (or default to 8.0)
DOTNET_VERSION = '8.0'
serverCsprojDir = os.path.join(PROJECT_BACKEND_DIR, 'Server/Server.csproj')
if os.path.exists(serverCsprojDir):
  with open(serverCsprojDir) as f:
    # Load the .csproj file
    tree = ElementTree.parse(serverCsprojDir)
    root = tree.getroot()

    # Find the <TargetFramework> element
    target_framework_element = root.find('.//TargetFramework')

    # Check if the element exists
    if target_framework_element is not None:
        # Get the text content of the <TargetFramework> element
        target_framework = target_framework_element.text
        DOTNET_VERSION = target_framework.replace('net', '') # drop the net prefix, so 'net8.0' becomes '8.0'
        print(f'Detected DOTNET_VERSION={DOTNET_VERSION} from {serverCsprojDir}')
    else:
        parser.warning(f'Unable to read <TargetFramework> from {serverCsprojDir}')
else:
  print(f'Unable to find "{serverCsprojDir}" for auto-detecting .NET version, using the default DOTNET_VERSION={DOTNET_VERSION}')

## Helper methods

async def httpGetRequest(log: logging.Logger, url: str) -> str:
  # \todo [petri] make async
  response = urllib.request.urlopen(url)
  log.debug(f'{url} returned {response.getcode()}')
  if response.getcode() >= 200 and response.getcode() < 300:
    return response.read().decode('utf-8')
  else:
    raise Exception(f'Got code {response.getcode()} when requesting url {url}')

def parsePrometheusMetric(line: str) -> Tuple[str, float]:
  [name, value] = line.split(' ')
  return (name, float(value))

async def fetchPrometheusMetrics(log: logging.Logger, url: str) -> List[Tuple[str, float]]:
  response = await httpGetRequest(log, url)
  lines = response.split('\n')
  lines = [line for line in lines if not line.startswith('#') and line != '']
  # if DEBUG:
  #   print('\n'.join(lines))
  filtered = [line for line in lines if line.startswith('dotnet_cpu_time_total') or line.startswith('process_cpu_seconds_total') or line.startswith('game_connections_current')]
  # process_cpu_seconds_total
  # dotnet_memory_virtual
  # dotnet_memory_allocated
  # dotnet_total_memory_bytes
  # dotnet_gc_loh_size
  # game_connections_current{type="Tcp"} 0
  # dotnet_collection_count_total{generation="1"} 1
  # dotnet_collection_count_total{generation="2"} 1
  # dotnet_collection_count_total{generation="0"} 1
  metrics = {}
  for (name, value) in [parsePrometheusMetric(line) for line in filtered]:
    metrics[name] = value
  return metrics

async def testHttpSuccess(log: logging.Logger, url: str):
  try:
    response = urllib.request.urlopen(url)
    log.debug(f'{url} returned {response.getcode()}')
    if response.getcode() >= 200 and response.getcode() < 300:
      return True
  except urllib.error.HTTPError as e:
    if e.code == 503:
      log.debug(f'Server not yet ready')
    else:
      log.debug(f'Got HTTPError from {url}: {e}')
  except Exception as e:
    log.debug(f'Failed to fetch {url}: {e}')
  return False

async def httpPostRequest(log: logging.Logger, url: str, data=b''):
  try:
    # \todo [petri] make async
    request = urllib.request.Request(url, data=data) # provide data to use a POST
    response = urllib.request.urlopen(request)
    log.debug(f'{url} returned {response.getcode()}')
    if response.getcode() >= 200 and response.getcode() < 300:
      return True
  except Exception as e:
    log.error(f'Failed HTTP POST to {url}: {e}')
  return False

async def runDockerTask(log: logging.Logger, command: str):
  proc = await run_process(log, directory='.', command=command, pipe_stdin=False)
  if proc.returncode != 0:
    print(f'{Color.FAIL}Docker command "{command}" exited with code {proc.returncode}:{Color.ENDC}')
    raise TaskExecutionError(f'Docker task terminated with exit code {proc.returncode}', command, proc.stdout_lines)
  return proc

async def runDockerBuildTask(log: logging.Logger, command: str):
  cmd_prefix = 'docker build' if args.use_buildkit else 'docker buildx build --output=type=docker'
  await runDockerTask(log.getChild('server'), f'{cmd_prefix} {command}')

async def killDockerContainer(log: logging.Logger, container_name: str):
  try:
    _ = await run_process(log, directory='.', command=f'docker kill {container_name}', pipe_stdin=False, verbose=False)
    _ = await run_process(log, directory='.', command=f'docker rm {container_name}', pipe_stdin=False, verbose=False)
  except:
    pass

class BackgroundGameServer:
  def __init__(self, log, server_proc):
    self.log = log
    self.server_proc = server_proc
    self.metrics_task = None
    self.metrics_samples = []
    self.stop_event = asyncio.Event()

  async def start(self):
    # Start container
    self.server_task = asyncio.create_task(self.server_proc.run(), name='run-gameserver') # create task so process makes progress in background

    # Wait until server container is created to get port forwards
    await self._resolvePorts()

  async def _collectMetricsAsync(self):
    prewarm_time = 30.0 # wait 30sec until start accumulating metrics (initial)
    start_time = get_elapsed_seconds()
    prev_time = start_time
    prev_cpu_time_total = 0.0
    while not self.stop_event.is_set():
      try:
        try:
          await asyncio.wait_for(self.stop_event.wait(), timeout=5.0)
        except asyncio.TimeoutError:
          pass
        cur_time = get_elapsed_seconds()
        metrics = await fetchPrometheusMetrics(self.log, f'http://localhost:{self._metricsPort}/metrics')
        # print(metrics)
        cpu_time_total = metrics['process_cpu_seconds_total']
        concurrents = sum([metrics[name] for name in metrics if name.startswith('game_connections_current')])
        if concurrents >= 10:
          time_elapsed = cur_time - prev_time
          cpu_usage_cores = (cpu_time_total - prev_cpu_time_total) / time_elapsed # number of cores busy (per second)
          concurrents_per_cpu = concurrents / cpu_usage_cores
          use_sample = prev_time - start_time >= prewarm_time # collect samples after pre-warm time has passed
          self.log.info(f'[{cur_time - start_time:.1f}s] Concurrents={int(concurrents)} CPU={cpu_usage_cores:.3f}cores/s CCU/core={concurrents_per_cpu:0.1f} ({"use" if use_sample else "skip"})')
          if use_sample:
            self.metrics_samples.append((time_elapsed, concurrents, cpu_usage_cores))

        prev_time = cur_time
        prev_cpu_time_total = cpu_time_total
      except Exception as e:
        self.log.error(f'ERROR: Exception caught while collecting metrics: {e}')
        traceback.print_exc()

  def startCollectingMetrics(self):
    self.metrics_task = asyncio.create_task(self._collectMetricsAsync())

  def summarizeMetrics(self):
    num_samples = len(self.metrics_samples)
    if num_samples == 0:
        self.log.info(f'***** Samples={num_samples} Concurrents=(n/a) CPU=(n/a)cores CCU/core=(n/a)')
        return

    total_time_elapsed = 0.0
    total_concurrents = 0
    total_cpu_usage_cores = 0.0
    for (time_elapsed, concurrents, cpu_usage_cores) in self.metrics_samples:
      total_time_elapsed += time_elapsed
      total_concurrents += concurrents
      total_cpu_usage_cores += cpu_usage_cores
    avg_concurrents = total_concurrents / num_samples
    avg_cpu_usage_cores = total_cpu_usage_cores / num_samples
    concurrents_per_core = avg_concurrents / avg_cpu_usage_cores
    self.log.info(f'***** Samples={num_samples} Concurrents={avg_concurrents:.1f} CPU={total_cpu_usage_cores:.2f}cores CCU/core={concurrents_per_core}')

  async def _resolvePorts(self):
    start_time = get_elapsed_seconds()
    # Container is created asynchronouly, wait for inspect to succeed
    while True:
      proc = await run_process(self.log, directory='.', command=f'docker inspect {SERVER_CONTAINER_NAME}', pipe_stdin=False, verbose=False)
      if proc.returncode == 0:
        try:
          inspect_data = json.loads(" ".join(proc.stdout_lines))
          # Path may not be available and may throw
          systemPort = int(inspect_data[0]["NetworkSettings"]["Ports"]["8888/tcp"][0]["HostPort"])
          probePort = int(inspect_data[0]["NetworkSettings"]["Ports"]["8585/tcp"][0]["HostPort"])
          metricsPort = int(inspect_data[0]["NetworkSettings"]["Ports"]["9090/tcp"][0]["HostPort"])
          # Wait for ports to be allocated
          if systemPort != 0 and probePort != 0 and metricsPort != 0:
            self._systemPort = systemPort
            self._probePort = probePort
            self._metricsPort = metricsPort
            return
        except Exception as e:
          self.log.error(e)
          pass
      else:
        # Check if server died unexpectedly during init
        if self.server_task.done():
          self.log.error(f'Server exited unexpectedly while waiting for it to start: <<<{self.server_proc.get_output()}>>>')
          raise Exception('Server exited unexpectedly while waiting for it to to start!')

      await asyncio.sleep(0.5)

      # Timeout after 1 minute
      if get_elapsed_seconds() > start_time + 60:
        raise TimeoutError("Container did not become inspectable within a time limit")

  async def waitForReady(self):
    start_time = get_elapsed_seconds()
    # Wait for server /isReady to return success
    while True:
      # self.log.debug('Check server up')
      if await testHttpSuccess(self.log, f'http://localhost:{self._probePort}/isReady'):
        self.log.info(f'Server is ready!')
        break
      else:
        # Check if server died unexpectedly during init
        if self.server_task.done():
          self.log.error(f'Server exited unexpectedly while waiting for it to be ready: <<<{self.server_proc.get_output()}>>>')
          raise Exception('Server exited unexpectedly while waiting for it to be ready!')
        await asyncio.sleep(0.5)
      # Timeout after 1 minute
      if get_elapsed_seconds() > start_time + 60:
        raise TimeoutError("Server did not become ready within a time limit")

  async def waitFinished(self):
    await asyncio.wait([self.server_task])

  async def stop(self):
    # stop logs from being output to console (this avoid burying the real error with the server shutdown sequence)
    print(f'VERBOSE = {self.server_proc.verbose}')
    self.server_proc.verbose = False

    # stop metrics collector
    self.stop_event.set()

    # wait for metrics collection to stop
    await asyncio.wait([self.metrics_task])

    # print('Sending SIGTERM')
    # self.server_proc.proc.terminate()
    # await asyncio.sleep(2)
    self.log.info('Requesting gameserver graceful shutdown')
    await httpPostRequest(self.log, f'http://localhost:{self._systemPort}/gracefulShutdown')
    self.log.info('Killing docker container') # \todo [petri] use SIGTERM instead?
    await runDockerTask(self.log, f'docker kill {SERVER_CONTAINER_NAME}')
    self.log.info('Waiting for gameserver to exit')
    await self.waitFinished()

async def startGameServer(log: logging.Logger):
  # Kill old server in case it exists
  await killDockerContainer(log, SERVER_CONTAINER_NAME)

  # Start the server
  log.info('Start game server container')
  cmd = ' '.join([
    'docker run',
    '--rm',
    f'--name {SERVER_CONTAINER_NAME}',
    f'-e METAPLAY_ENVIRONMENT_FAMILY=Local',
    f'-p 127.0.0.1:0:8585',
    f'-p 127.0.0.1:0:8888',
    f'-p 127.0.0.1:0:9090',
    f'{SERVER_IMAGE_NAME}',
    f'gameserver',
    f'-LogLevel=Information',
    f'{METAPLAY_OPTS}',
    f'{METAPLAY_SERVER_OPTS}'
  ])
  server_proc = AsyncProcess(log, directory='.', command=cmd, pipe_stdin=False)
  gameserver = BackgroundGameServer(log, server_proc)

  # Wait until server is ready & start collecting metrics
  try:
    await gameserver.start()
    await gameserver.waitForReady()
  except:
    await killDockerContainer(log, SERVER_CONTAINER_NAME)
    raise

  gameserver.startCollectingMetrics()
  return gameserver

async def runBotClient(log: logging.Logger, duration: str, max_bots: int, spawn_rate: int, session_duration: str) -> None:
  await killDockerContainer(log, BOTCLIENT_CONTAINER_NAME)
  cmd = ' '.join([
    'docker run',
    '--rm',
    f'--name {BOTCLIENT_CONTAINER_NAME}',
    f'--network container:{SERVER_CONTAINER_NAME}',
    f'-e METAPLAY_ENVIRONMENT_FAMILY=Local',
    f'{SERVER_IMAGE_NAME}',
    f'botclient',
    f'-LogLevel=Information',
    f'{METAPLAY_OPTS}',
    f'--Bot:ServerHost=localhost',
    f'--Bot:ServerPort=9339',
    f'--Bot:EnableTls=false',
    f'--Bot:CdnBaseUrl=http://localhost:5552/',
    f'-ExitAfter={duration}',
    f'-MaxBots={max_bots}',
    f'-SpawnRate={spawn_rate}',
    f'-ExpectedSessionDuration={session_duration}'
  ])
  await runDockerTask(log, cmd)

async def runDashboardPlaywrightTests(log: logging.Logger):
  await killDockerContainer(log, PLAYWRIGHT_TS_CONTAINER_NAME)
  RESULTS_DIR = os.path.abspath(args.results_dir).replace('\\', '/')
  cmd = ' '.join([
    'docker run',
    '--rm',
    f'-e DASHBOARD_BASE_URL=http://localhost:5550',
    f'--name {PLAYWRIGHT_TS_CONTAINER_NAME}',
    f'-e CI=true',
    f'-e OUTPUT_DIRECTORY=/PlaywrightOutput',
    f'--network container:{SERVER_CONTAINER_NAME}',
    f'-v {RESULTS_DIR}/playwright:/PlaywrightOutput',
    f'{PLAYWRIGHT_TS_IMAGE_NAME}'
  ])
  await runDockerTask(log, cmd)

async def runPlaywrightTests(log: logging.Logger):
  await killDockerContainer(log, PLAYWRIGHT_NET_CONTAINER_NAME)
  RESULTS_DIR = os.path.abspath(args.results_dir).replace('\\', '/')
  # \todo enable --ipc=host as Chrome apparently consumes a lot of memory without it -- this doesn't work on BitBucket, though, so need to add a flag/detection for that
  cmd = ' '.join([
    'docker run',
    '--rm',
    f'-e DASHBOARD_BASE_URL=http://localhost:5550',
    f'--name {PLAYWRIGHT_NET_CONTAINER_NAME}',
    f'-e OUTPUT_DIRECTORY=/PlaywrightOutput',
    f'--network container:{SERVER_CONTAINER_NAME}',
    f'-v {RESULTS_DIR}/playwright:/PlaywrightOutput',
    f'{PLAYWRIGHT_NET_IMAGE_NAME}'
  ])
  await runDockerTask(log, cmd)

## Tests

# TEST CASE: Build images

async def testBuildImage(log: logging.Logger):
  # Build server image
  docker_build_args = ' '.join([
    f'--pull',
    f'--build-arg SDK_ROOT={METAPLAY_SDK_DIR}',
    f'--build-arg PROJECT_ROOT={args.project_dir}',
    f'--build-arg BACKEND_DIR={args.backend_dir}',
    f'--build-arg DOTNET_VERSION={DOTNET_VERSION}',
    f'-f {METAPLAY_SDK_DIR}/Dockerfile.server'
  ])
  await runDockerBuildTask(log.getChild('server'), f'-t {SERVER_IMAGE_NAME} {docker_build_args} .')

  # Build Playwright TS core test runner image.
  await runDockerBuildTask(log.getChild('playwright-ts'), f'-t {PLAYWRIGHT_TS_IMAGE_NAME} --target playwright-ts-tests {docker_build_args} .')

  # Build Playwright.NET image test runner image.
  await runDockerBuildTask(log.getChild('playwright-net'), f'-t {PLAYWRIGHT_NET_IMAGE_NAME} --target playwright-net-tests {docker_build_args} .')

# TEST CASE: Run bots

async def testBots(log: logging.Logger):
  gameserver = await startGameServer(log.getChild('server'))
  try:
    await runBotClient(log.getChild('bots'), duration='00:02:00', max_bots=100, spawn_rate=30, session_duration='00:00:30')
    gameserver.summarizeMetrics()
  finally:
    await gameserver.stop()

# TEST CASE: Dashboard (Playwright TS) tests

async def testDashboardWithPlaywright(log: logging.Logger):
    gameserver = await startGameServer(log.getChild("server"))
    try:
        await runDashboardPlaywrightTests(log.getChild("tests"))
    finally:
        await gameserver.stop()

# TEST CASE: .NET System tests (using Playwright.NET)

async def testSystem(log: logging.Logger):
  gameserver = await startGameServer(log.getChild('server'))
  try:
    await runPlaywrightTests(log.getChild('tests'))
  finally:
    await gameserver.stop()

## Main

TEST_SPECS = [
  ('build-image', testBuildImage),
  ('test-bots', testBots),
  ('test-dashboard', testDashboardWithPlaywright),
  ('test-system', testSystem),
]

# Configure logging
logging.basicConfig(
  level=logging.INFO if args.quiet else logging.DEBUG,
  format='[%(asctime)s.%(msecs)03d %(levelname)s %(name)s] %(message)s',
  datefmt='%H:%M:%S')

async def main():
  # Check docker is running.
  docker_check_proc = await run_process(logging.getLogger("docker_check"), directory='.', command="docker version", pipe_stdin=False, verbose=False)
  if docker_check_proc.returncode != 0:
    print(f'{Color.FAIL}Could not connect to docker engine. Check docker is installed and docker engine is running')
    sys.exit(1)

  for (test_name, test_fn) in TEST_SPECS:
    log = logging.getLogger(test_name)
    should_run = len(args.tests) == 0 or test_name in args.tests
    if should_run:
      try:
        log.info(f'{Color.OKCYAN}Running test: {test_name}{Color.ENDC}')
        await test_fn(log)
        log.info(f'{Color.OKGREEN}Test {test_name} success{Color.ENDC}')
      except TaskExecutionError as e:
        # Print the logs from the failed task invocation (it's good to have these visible last in the logs)
        prefixed_logs = prefix_lines(e.logs, '> ')
        print(f'LOGS FROM FAILED TASK: {e.command}\n{prefixed_logs}\n')
        print(f'{Color.FAIL}Test {test_name} failed -- see above for logs!\n  Message: {e.message}{Color.ENDC}\n  Invoked command: {e.command}')
        sys.exit(1)
      except Exception as e:
        print(f'{Color.FAIL}Test {test_name} failed with: {e}{Color.ENDC}')
        traceback.print_exc() # print the stack trace so we know what failed
        sys.exit(1)
    else:
      log.warning(f'Skip test: {test_name}')

if __name__ == '__main__':
  colorama.init()
  try:
    asyncio.run(main())
  except Exception as e:
    print(e.message)
