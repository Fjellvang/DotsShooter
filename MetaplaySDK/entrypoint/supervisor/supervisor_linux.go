// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

//go:build linux || darwin

package supervisor

import (
	"log"
	"os"
	"os/exec"
	"os/signal"
	"strings"
	"syscall"
)

type completedProcess struct {
	Pid        int
	WaitStatus syscall.WaitStatus
	Rusage     syscall.Rusage
}

func pollDeadChildProcesses(output chan completedProcess) {
	sigChldChan := make(chan os.Signal, 10)
	signal.Notify(sigChldChan, syscall.SIGCHLD)

	for {
		// Reap all dead children. There can be multiple, or
		// zero (if previous reap reaped the children the signal was for).
		//
		// In case we missed a signal before registering it, reap once before
		// waiting for the signal.
		for {
			wstatus := syscall.WaitStatus(0)
			rusage := syscall.Rusage{}
			terminatedChildPid, err := syscall.Wait4(-1, &wstatus, 0, &rusage)
			if err != nil {
				// Assume error is that there are no dead processes. Go
				// back to wait for next signal
				break
			}

			output <- completedProcess{terminatedChildPid, wstatus, rusage}
		}

		// Wait for signal
		<-sigChldChan
	}
}

func waitForMainProcess(process *os.Process, sigTermChan chan os.Signal, completedProcessChan chan completedProcess) completedProcess {
	// Forward SIGTERMs to the main process
	// \note: We don't use cmd.Wait() for the process as that may interefere or be
	//        interfered by the zombie process reaping logic
	for {
		select {
		case <-sigTermChan:
			// Forward SIGTERM
			_ = process.Signal(syscall.SIGTERM)

		case child := <-completedProcessChan:
			if child.Pid == process.Pid {
				return child
			}
		}
	}
}

func RunChildProcess(workingDir string, binaryName string, appArgs []string) int {
	err := os.Chdir(workingDir)
	if err != nil {
		log.Fatalf("Failed enter directory %s: %s\n", workingDir, err)
	}

	// Exec() wants the program name as the first argument
	log.Printf("Running %s %s\n", binaryName, strings.Join(appArgs, " "))

	// Capture SIGTERM
	sigTermChan := make(chan os.Signal, 10)
	signal.Notify(sigTermChan, syscall.SIGTERM)

	// Capture and handle all SIGCHLDs
	completedProcessChan := make(chan completedProcess, 1)
	go pollDeadChildProcesses(completedProcessChan)

	// Execute the binary with current environment & out stdout/stderr
	cmd := exec.Command("./"+binaryName, appArgs...)
	cmd.Env = os.Environ()
	cmd.Stdout = os.Stdout
	cmd.Stderr = os.Stderr

	err = cmd.Start()
	if err != nil {
		log.Fatalf("Failed to start dotnet binary: %s\n", err)
		os.Exit(1)
	}

	// Wait for process to complete while forwarding SIGTERM
	deadProcess := waitForMainProcess(cmd.Process, sigTermChan, completedProcessChan)

	// Process completed. Print result
	if deadProcess.WaitStatus.CoreDump() {
		log.Fatalf("The child process crashed with a core dump")
	} else if deadProcess.WaitStatus.ExitStatus() != 0 {
		log.Fatalf("The child process exited with code %d\n", deadProcess.WaitStatus.ExitStatus())
	}

	log.Printf("Child process exited with code %d\n", deadProcess.WaitStatus.ExitStatus())
	return deadProcess.WaitStatus.ExitStatus()
}
