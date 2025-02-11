// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { useGameServerApiStore } from './gameServerApiStore'

/**
 * Callback function that is called with message data when an event is received
 * @param data Message data object that was received
 */
type MessageCallback = (data: object) => void

/**
 * SseHandler class to simplify handling server sent events
 *
 * Usage:
 *  sse = new SseHandler('/api/sse-endpoint')
 *  sse.addMessageHandler('myMessage', data => console.log(data))
 *  sse.start()
 */
export class SseHandler {
  private readonly endpoint: string
  private eventSource: EventSource | null
  private fnMessageHandlers: Record<string, MessageCallback | undefined>
  private hasStarted: boolean

  /**
   * @param endpoint API endpoint address to connect to (eg: '/api/sse')
   */
  public constructor(endpoint: string) {
    this.endpoint = endpoint
    this.eventSource = null
    this.fnMessageHandlers = {}
    this.hasStarted = false

    // Note: This is needed because Firefox seems to otherwise treat the SSE connection as interrupted and throw an error.
    window.onbeforeunload = (): void => {
      console.log('Gracefully closing SSE connection before page unload.')
      this.stop()
    }
  }

  /**
   * Add a handler for a message type
   * @param messageName Name of the message to handle
   * @param fnMessageHandler The callback to handle the message
   */
  public addMessageHandler(messageName: string, fnMessageHandler: MessageCallback): void {
    if (!this.fnMessageHandlers[messageName]) {
      this.fnMessageHandlers[messageName] = fnMessageHandler
    } else {
      console.error(`See message handler for ${messageName} already registered - ignoring`)
    }
  }

  /**
   * Open the connection and start receiving messages. Handlers for messages that were
   * added through addMessageHandler() will be automatically called if a matching
   * message is received
   */
  public async start(): Promise<void> {
    // Protect against `start` being called multiple times.
    if (this.hasStarted) return
    this.hasStarted = true

    // eslint-disable-next-line @typescript-eslint/no-this-alias -- Refactor this later. Not urgent now.
    const _this = this

    // A periodically run watcher that will open the connection and then re-open the it if it closes.
    function sseWatcher(): void {
      if (_this.eventSource) return

      // Create the event stream connection.
      _this.eventSource = new EventSource(_this.endpoint)

      // Subscribe to messages form the event stream.
      _this.eventSource.onmessage = (messageEvent: MessageEvent<string>): void => {
        try {
          const data = JSON.parse(messageEvent.data) as { name?: string; value: object }

          const messageName = data.name
          if (messageName === undefined) {
            console.error(`SSE message had no name field: ${JSON.stringify(data)}`)
          } else {
            if (_this.fnMessageHandlers[messageName]) {
              try {
                _this.fnMessageHandlers[messageName](data.value)
              } catch (err) {
                console.error(
                  `SSE handler failed to handle message ${messageName} with payload ${messageEvent.data}: ${String(err)}`
                )
              }
            } else {
              console.warn(`There is no handler for SSE message ${messageName} with payload ${messageEvent.data}`)
            }
          }
        } catch (err) {
          console.error(`Failed to parse SSE message data: ${messageEvent.data}, reason: ${String(err)}`)
        }
      }

      // Listen for the connection opening.
      _this.eventSource.onopen = (): void => {
        // We are now connected.
        useGameServerApiStore().isConnected = true
      }

      // Listen for errors.
      _this.eventSource.onerror = (errorEvent): void => {
        if (errorEvent.eventPhase === EventSource.CLOSED) {
          // We are now *not* connected. Set things to reflect that, causing automatic retries later.
          _this.eventSource = null
          useGameServerApiStore().isConnected = false
        }
      }
    }

    // Check on the SSE handler every two seconds.
    window.setInterval(sseWatcher, 2000)
  }

  /**
   * Close the connection and stop receiving messages
   */
  public stop(): void {
    if (this.eventSource) {
      this.eventSource.close()
      this.eventSource = null
      useGameServerApiStore().isConnected = false
    }
  }
}
