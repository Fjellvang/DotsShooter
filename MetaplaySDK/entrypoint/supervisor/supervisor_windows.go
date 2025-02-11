// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

//go:build windows

// Note: On Windows, only naive supervision of the child process is done.
// This is only supported to make development of new features easier.

package supervisor

import (
	"log"
	"os"
	"os/exec"
	"strings"
)

func RunChildProcess(workingDir string, binaryName string, appArgs []string) int {
	err := os.Chdir(workingDir)
	if err != nil {
		log.Fatalf("Failed enter directory %s: %s\n", workingDir, err)
	}

	// Exec() wants the program name as the first argument
	log.Printf("Running %s %s\n", binaryName, strings.Join(appArgs, " "))

	// Execute the binary with current environment & out stdout/stderr
	cmd := exec.Command(binaryName, appArgs...)
	cmd.Env = os.Environ()
	cmd.Stdout = os.Stdout
	cmd.Stderr = os.Stderr

	err = cmd.Start()
	if err != nil {
		log.Fatalf("Failed to start dotnet binary: %s\n", err)
	}

	// Wait for process to complete while forwarding SIGTERM
	err = cmd.Wait()
	if err == nil {
		log.Print("Command executed successfully with exit code 0\n")
		return 0
	} else {
		if exitError, ok := err.(*exec.ExitError); ok {
			// The program has exited with an exit code != 0
			log.Printf("Exit Code: %d\n", exitError.ExitCode())
			return exitError.ExitCode()
		}
	}

	// There was an error starting the command or other errors
	log.Fatalf("Error running child process: %v\n", err)
	return 999
}
