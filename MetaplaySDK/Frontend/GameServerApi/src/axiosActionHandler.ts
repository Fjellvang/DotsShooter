// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import axios from 'axios'
import type { AxiosError, AxiosResponse, CancelTokenSource } from 'axios'

import type { ActionHandler } from './actionDebouncer'
import { useGameServerApi } from './gameServerApi'

const gameServerApi = useGameServerApi()

export interface AxiosActionHandlerRequestData<TRequestDataType> {
  /**
   * Optional URL to make the request to. If not provided, the URL must be provided in the action handler setup
   * function.
   */
  url?: string
  /**
   * Optional HTTP method to use for the request. If not provided, the method must be provided in the action handler
   * setup function.
   */
  method?: string
  /**
   * Optional query string to append to the URL.
   */
  queryString?: string
  /**
   * Optional payload data for the request.
   */
  data?: TRequestDataType
}

/**
 * Create an action handler that makes requests using Axios. Both URL and Method can be provided in the request data
 * or in the setup function. Options in the request data will take precedence.
 * @param url Optional URL to make the request to. This can be left blank if the URL is provided in the request data.
 * @param method Optional HTTP method to use for the request. This can be left blank if the method is provided in the request data.
 * @returns An `ActionHandler` to use with an `ActionDebouncer`.
 */
export function makeAxiosActionHandler<TRequestDataType, TResponseDataType>(
  url?: string,
  method?: string
): ActionHandler<
  AxiosActionHandlerRequestData<TRequestDataType>,
  AxiosResponse<TResponseDataType>,
  AxiosError,
  CancelTokenSource
> {
  const actionHandler: ActionHandler<
    AxiosActionHandlerRequestData<TRequestDataType>,
    AxiosResponse<TResponseDataType>,
    AxiosError,
    CancelTokenSource
  > = {
    setup: (requestData, response, error) => {
      // Create a cancel token that we can use to cancel the request.
      const cancelTokenSource = axios.CancelToken.source()

      // What URL/method should we use for the request?
      const requestUrl = requestData.url ?? url
      const requestMethod = requestData.method ?? method
      console.assert(requestUrl !== undefined, 'URL must be defined')
      console.assert(requestMethod !== undefined, 'Method must be defined')

      // Append the query string to the URL if it exists.

      const fullUrl = requestUrl + (requestData.queryString ? `?${requestData.queryString}` : '')

      // Make the request.
      gameServerApi
        .request({
          url: fullUrl,
          method: requestMethod,
          cancelToken: cancelTokenSource.token,
          data: requestData.data,
        })
        .then((data: AxiosResponse<TResponseDataType>) => {
          // Call the response callback with the data.
          response(data)
        })
        .catch((data: AxiosError) => {
          // Call the error callback with the data, unless it was the result of a cancel.
          if (!axios.isCancel(data)) {
            error(data)
          }
        })
      return cancelTokenSource
    },
    cancel: (token) => {
      // Cancel the in-flight request.
      token.cancel('Request canceled by user.')
    },
  }
  return actionHandler
}
