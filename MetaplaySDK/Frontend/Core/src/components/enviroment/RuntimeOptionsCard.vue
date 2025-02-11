<template lang="pug">
MCollapseCard(
  v-for="options in runtimeOptions"
  :key="options.name"
  :title="options.name.replace(/([a-z])([A-Z])/g, `$1 $2`)"
  :badge="Object.keys(options.values).length"
  class="tw-mx-auto tw-mb-4 tw-max-w-2xl"
  data-testid="runtime-option-collapse-header"
  )
  template(#header-right)
    MBadge {{ options.isStatic ? 'Static' : 'Dynamic' }}

  template(#subtitle)
    span(
      v-if="options.description"
      v-html="markdown(options.description)"
      )
    span(
      v-else
      class="tw-italic"
      ) No description available.

  MList
    MListItem(
      v-for="(value, key) in options.values"
      :key="key"
      striped
      class="tw-px-4"
      )
      //- Key.
      span(
        class="tw-rounded tw-px-1.5 tw-py-0.5 tw-text-xs tw-text-neutral-700"
        :style="`background: ${sourceColors[options.sources[fixupKeyCasing(key)]] || 'lightgrey'}`"
        ) {{ fixupKeyCasing(key) }}

      template(#top-right)
        //- Source.
        span {{ options.sources[fixupKeyCasing(key)] }}

      template(#bottom-left)
        //- Description.
        div(class="tw-mb-1.5")
          span(
            v-if="options.descriptions[fixupKeyCasing(key)]"
            v-html="markdown(options.descriptions[fixupKeyCasing(key)])"
            )
          span(
            v-else
            class="tw-italic"
            ) No description available.

        //- Value.
        MBadge(
          v-if="value === null"
          size="large"
          ) null
        MBadge(
          v-if="value === ''"
          size="large"
          ) empty string
        MBadge(
          v-else-if="value === true"
          variant="success"
          size="large"
          ) true
        MBadge(
          v-else-if="value === false"
          variant="danger"
          size="large"
          ) false
        pre(v-else-if="Array.isArray(value) || typeof value === 'object'") {{ value }}
        span(v-else) {{ value }}

meta-raw-data(
  :kvPair="runtimeOptions"
  name="runtimeOptions"
  )
</template>

<script lang="ts" setup>
import { cloneDeep } from 'lodash-es'
import { marked } from 'marked'
import { computed } from 'vue'

import { MBadge, MCollapseCard, MList, MListItem } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getRuntimeOptionsSubscriptionOptions } from '../../subscription_options/general'

function createSourceColors(sources: any): Record<string, string> {
  const step = 360.0 / sources.length
  const colors: any = {}
  for (let ndx = 0; ndx < sources.length; ndx++) {
    const src = sources[ndx]
    if (ndx === 0) {
      // First item (default value) uses gray
      colors[src.name] = 'rgba(0, 0, 0, 0.15)'
    } else {
      const angle = Math.round(30.0 + ndx * step) % 360
      colors[src.name] = `hsl(${angle}, 60%, 85%)`
    }
  }
  return colors
}
function createTooltips(sources: any): Record<string, string> {
  return Object.assign(
    {},
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    ...sources.map((source: any) => ({ [source.name]: source.description }))
  )
}

const { data: runtimeOptionsData } = useSubscription(getRuntimeOptionsSubscriptionOptions())

const runtimeOptions = computed(() => {
  if (runtimeOptionsData.value) {
    let options = cloneDeep(runtimeOptionsData.value.options)

    // Sort options by name.
    options = options.sort((a: any, b: any) => {
      const nameA = a.name.toUpperCase()
      const nameB = b.name.toUpperCase()
      if (nameA < nameB) {
        return -1
      } else if (nameA > nameB) {
        return 1
      } else {
        return 0
      }
    })

    // We don't want to show the C# '$type' value here. It's added by the serializer but it makes no sense to show
    // it to users here. We'll recursively remove it from the data.
    const removeTypes = (values: any): any => {
      delete values.$type
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      Object.entries(values).forEach((entry: any) => {
        if (entry[1] !== null && typeof entry[1] === 'object') {
          removeTypes(entry[1])
        }
      })
    }
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    Object.entries(options).forEach((option: any) => {
      removeTypes(option[1].values)
    })

    return options
  }
  return null
})

const sourceColors = computed(() => {
  return createSourceColors(runtimeOptionsData.value.allSources)
})

const tooltips = computed(() => {
  return createTooltips(runtimeOptionsData.value.allSources)
})

function fixupKeyCasing(key: any): string {
  // TODO Fix this at source in the runtimeOptions API endpoint
  return key.charAt(0).toUpperCase() + key.slice(1)
}

function markdown(source: string): string | Promise<string> | undefined {
  const result = marked.parseInline(source)

  if (typeof result === 'string') {
    return result
  }

  result
    .then((html: any) => {
      return html
    })
    .catch((error: any) => {
      console.error(error)
    })
}
</script>
