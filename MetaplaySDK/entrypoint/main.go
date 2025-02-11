// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

package main

import (
	"flag"
	"fmt"
	"log"
	"os"

	"metaplay.io/entrypoint/health_probe_proxy"
	"metaplay.io/entrypoint/supervisor"
)

func stringOrDefault(value string, defaultValue string) string {
	if value == "" {
		return defaultValue
	} else {
		return value
	}
}

func main() {
	// Parse command line arguments
	argWorkingDir := flag.String("working-dir", "", "Working directory where binary is")
	flag.Parse()

	// Check that the app name is given
	args := flag.Args() // Exclude the program name (os.Args[0])
	if len(args) < 1 {
		fmt.Println("Usage: entrypoint <app> [arguments...]")
		fmt.Println("  app must be either 'gameserver', 'botclient', or 'dotnet'")
		os.Exit(2)
	}

	// Start the Kubernetes health probe proxy
	go health_probe_proxy.Run()

	// Figure out binary to run
	appName := args[0]
	appArgs := args[1:]
	workingDir := *argWorkingDir

	// Run the desired application (server or botclient)
	switch appName {
	case "gameserver":
		log.Printf("Starting game server...\n")
		workingDir = stringOrDefault(workingDir, "/gameserver")
		childExitCode := supervisor.RunChildProcess(workingDir, "./Server", appArgs)
		os.Exit(childExitCode)

	case "botclient":
		log.Printf("Starting botclient...\n")
		workingDir = stringOrDefault(workingDir, "/botclient")
		childExitCode := supervisor.RunChildProcess(workingDir, "./BotClient", appArgs)
		os.Exit(childExitCode)

	case "dotnet":
		log.Printf("Starting dotnet...\n")
		workingDir = stringOrDefault(workingDir, ".")
		childExitCode := supervisor.RunChildProcess(workingDir, "dotnet", appArgs)
		os.Exit(childExitCode)

	default:
		log.Fatalf("Invalid appName: %s\n", appName)
		os.Exit(2)
	}
}
