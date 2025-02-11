<template lang="pug">
div(ref="container")
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'

import { plot, type PlotOptions } from '@observablehq/plot'

const props = defineProps<{
  /**
   * Plot options. We set some defaults like a neutral grey fill color and a sans-serif font.
   * https://observablehq.com/plot/features/plots
   */
  options: PlotOptions
}>()

/**
 * Container element for the plot.
 */
const container = ref<HTMLElement>()

// Responsive UI ------------------------------------------------------------------------------------------------------

const width = ref<number>()

// Watch the container for changes and update the width.
let resizeObserver: ResizeObserver | undefined
onMounted(() => {
  if (!container.value) {
    return
  }

  width.value = container.value.clientWidth

  resizeObserver = new ResizeObserver(() => {
    width.value = container.value?.clientWidth
  })

  resizeObserver.observe(container.value)
})
onUnmounted(() => {
  if (resizeObserver) {
    resizeObserver.disconnect()
  }
})

// Plot ---------------------------------------------------------------------------------------------------------------

/**
 * Modify the Plot options to include some defaults.
 */
const modifiedOptions = computed(() => {
  const baseOptions: PlotOptions = {}
  // Set width to full.
  baseOptions.width = width.value
  // Set style.
  baseOptions.style = {
    fill: '#737373', // This affects all scale text labels and marks. Neutral grey.
    fontFamily:
      'ui-sans-serif, system-ui, sans-serif, "Apple Color Emoji", "Segoe UI Emoji", "Segoe UI Symbol", "Noto Color Emoji"',
  }
  return {
    ...baseOptions,
    ...props.options,
  }
})

/**
 * Watch the options for changes and re-render Plot as needed.
 */
const renderedPlot = computed(() => {
  return plot(modifiedOptions.value)
})

/**
 * Watch the rendered plot for changes and update the container as needed.
 * Plot outputs a pre-rended SVG or HTML element.
 */
watch(renderedPlot, (newValue) => {
  if (container.value) {
    // Remove old plot.
    container.value.innerHTML = ''
    // Append new plot.
    container.value.append(newValue)
  }
})

onMounted(() => {
  // Append plot to container.
  container.value?.append(renderedPlot.value)
})
</script>
