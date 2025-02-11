// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

package health_probe_proxy

import (
	"fmt"
	"io"
	"log"
	"net/http"
	"sync"
	"time"
)

// Proxy configuration
const listenPort = 8585
const targetBaseUrl = "http://127.0.0.1:8888"

// OverrideMode represents the possible states of override
type OverrideMode int

const (
	Passthrough  OverrideMode = iota // Default state, passthrough the probes to the target binary
	ForceSuccess                     // Override to force success
	ForceFailure                     // Override to force failure
)

// OverrideState represents the state of override for an endpoint
type OverrideState struct {
	mu       sync.Mutex
	mode     OverrideMode
	expireAt time.Time
}

// SetOverride sets the override state for the endpoint
func (o *OverrideState) SetOverride(mode OverrideMode) {
	o.mu.Lock()
	defer o.mu.Unlock()
	o.mode = mode
	o.expireAt = time.Now().Add(30 * time.Minute)
	log.Printf("Override expires at %v\n", o.expireAt)
}

// GetState returns the current state of the override
func (o *OverrideState) GetState() (OverrideMode, time.Duration) {
	o.mu.Lock()
	defer o.mu.Unlock()

	// If override has expired, reset back to Passthrough
	now := time.Now()
	if o.mode != Passthrough && time.Now().After(o.expireAt) {
		log.Printf("Resetting expired override: %v vs %v\n", now, o.expireAt)
		o.mode = Passthrough
		o.expireAt = time.Time{}
	}

	return o.mode, o.expireAt.Sub(now)
}

var (
	healthzOverride = &OverrideState{}
	isReadyOverride = &OverrideState{}
)

func Run() {
	http.HandleFunc("/healthz", createProxyHandler(fmt.Sprintf("%s/healthz", targetBaseUrl), healthzOverride))
	http.HandleFunc("/isReady", createProxyHandler(fmt.Sprintf("%s/isReady", targetBaseUrl), isReadyOverride))
	http.HandleFunc("/setOverride/", setOverrideHandler)
	log.Printf("Serving probe proxies on port %d, targetBaseUrl=%s", listenPort, targetBaseUrl)
	http.ListenAndServe(fmt.Sprintf(":%d", listenPort), nil)
}

func createProxyHandler(targetUrl string, override *OverrideState) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		// Handle the endpoint status override
		mode, expiresIn := override.GetState()
		switch mode {
		case ForceSuccess:
			w.WriteHeader(http.StatusOK)
			fmt.Fprintf(w, "Probe override to forced success, expires in %s\n", formatDuration(expiresIn))
			log.Printf("Returning forced success for %s, expires in %s\n", targetUrl, formatDuration(expiresIn))
			return
		case ForceFailure:
			retStr := fmt.Sprintf("Probe override to forced failure, expires in %s", formatDuration(expiresIn))
			http.Error(w, retStr, http.StatusInternalServerError)
			log.Printf("Returning forced failure for %s, expires in %s\n", targetUrl, formatDuration(expiresIn))
			return
		}

		// log.Printf("Forwarding check to %s\n", targetUrl)

		// Create a new request to the target server
		req, err := http.NewRequest("GET", targetUrl, nil)
		if err != nil {
			resStr := fmt.Sprintf("Failed to create health probe request to child process: %v", err)
			log.Println(resStr)
			http.Error(w, resStr, http.StatusInternalServerError)
			return
		}

		// Forward headers from the incoming request to the outgoing request
		req.Header = r.Header

		// Send the request to the target server
		client := http.Client{}
		resp, err := client.Do(req)
		if err != nil {
			resStr := fmt.Sprintf("Failed to send health probe request to child process: %v", err)
			log.Println(resStr)
			http.Error(w, resStr, http.StatusInternalServerError)
			return
		}
		defer resp.Body.Close()

		// Read the response body from the target server
		body, err := io.ReadAll(resp.Body)
		if err != nil {
			resStr := fmt.Sprintf("Failed to read health probe response from child process: %v", err)
			log.Println(resStr)
			http.Error(w, resStr, http.StatusInternalServerError)
			return
		}

		// Write the response received from the target server back to the client
		w.WriteHeader(resp.StatusCode)
		w.Write(body)
	}
}

func setOverrideHandler(w http.ResponseWriter, r *http.Request) {
	endpoint := r.URL.Path[len("/setOverride/"):]
	override := getOverrideState(endpoint)

	if override == nil {
		http.Error(w, fmt.Sprintf("Invalid endpoint: '%s'", endpoint), http.StatusNotFound)
		return
	}

	modeStr := r.URL.Query().Get("mode")
	mode, err := parseOverrideMode(modeStr)
	if err != nil {
		http.Error(w, fmt.Sprintf("Invalid mode parameter '%s'", modeStr), http.StatusBadRequest)
		return
	}

	override.SetOverride(mode)

	w.WriteHeader(http.StatusOK)
	fmt.Fprintf(w, "Override for endpoint '%s' set to '%s' for 30 minutes\n", endpoint, modeStr)
}

func getOverrideState(endpoint string) *OverrideState {
	switch endpoint {
	case "healthz":
		return healthzOverride
	case "isReady":
		return isReadyOverride
	default:
		return nil
	}
}

func parseOverrideMode(modeStr string) (OverrideMode, error) {
	switch modeStr {
	case "Success":
		return ForceSuccess, nil
	case "Failure":
		return ForceFailure, nil
	case "Passthrough":
		return Passthrough, nil
	default:
		return Passthrough, fmt.Errorf("invalid OverrideMode value")
	}
}

func formatDuration(d time.Duration) string {
	minutes := int(d.Minutes())
	seconds := int(d.Seconds()) % 60
	if minutes > 0 {
		return fmt.Sprintf("%dm %ds", minutes, seconds)
	} else {
		return fmt.Sprintf("%ds", seconds)
	}
}
