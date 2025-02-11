// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

/**
 * Defines a query expression for Grafana.
 */
export interface GrafanaQueryExpression {
  label: string
  filter: string
  expression: string
}

/**
 * Helper function to make a GrafanaQueryExpression object.
 * @param label Label for the expression, eg: 'loglevel'.
 * @param filter Filter for the expression, eg: '='.
 * @param expression The expression itself, eg 'ERR'
 */
export function makeGrafanaQueryExpression(label: string, filter: string, expression: string): GrafanaQueryExpression {
  return { label, filter, expression }
}

/**
 * Helper function to create Grafana query URIs.
 * TODO: This should be rolled out to other Grafana links but that is beyond the scop of the current PR.
 * @param additionalQueryExpressions Expressions to add to the query.
 * @param from Date or string for the start of the query. Supports Grafana date types such as "now-1h".
 * @param to Date or string for the end of the query. Supports Grafana date types such as "now+1h".
 * @returns Fully formed URI to the Grafana query or `undefined` if Grafana is not enabled in this deployment.
 */
// eslint-disable-next-line @typescript-eslint/max-params
export function makeGrafanaUri(
  grafanaUri: string,
  kubernetesNamespace: string | undefined,
  additionalQueryExpressions: GrafanaQueryExpression[],
  from: Date | string,
  to: Date | string
): string {
  // Join custom queryExpressions together with those that are required by default.
  const queryExpressionsObject: GrafanaQueryExpression[] = [makeGrafanaQueryExpression('app', '=', 'metaplay-server')]
  if (kubernetesNamespace) {
    queryExpressionsObject.push(
      makeGrafanaQueryExpression(
        'namespace',
        '=',

        kubernetesNamespace
      )
    )
  }
  additionalQueryExpressions.forEach((queryExpression) => queryExpressionsObject.push(queryExpression))

  // Expressions for queries are passed as a string of comma separated values.
  const queryExpressionsString = queryExpressionsObject.reduce((acc, cur) => {
    return `${acc}${acc ? ',' : ''}${cur.label}${cur.filter}"${cur.expression}"`
  }, '')

  // The main expression looks enough like a JSON object that we can treat it as one and then stringify it.
  const query = {
    datasource: 'Loki',
    queries: [
      {
        expr: `{${queryExpressionsString}}`,
      },
    ],
    range: {
      from,
      to,
    },
  }
  const queryString = JSON.stringify(query)

  // Formulate the whole URI.
  return `${grafanaUri}/explore?orgId=1&left=${queryString}`
}
