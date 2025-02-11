<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(:is-loading="allClassTypeOptions.length === 0 && allSampleTypeOptions.length === 0")
  template(#overview)
    MPageOverviewCard(title="Generated UI Testing Tool")
      p Generated UI dynamically creates forms and views from your in-game data.
      p(class="tw-text-xs+ tw-text-neutral-400") Select a C# class type to preview how the generated form and generated view components will look on the LiveOps Dashboard.
        |
        | You can use this to debug your game-specific types. Check out the #[MTextButton(to="https://docs.metaplay.io/liveops-dashboard/how-to-guides/working-with-generated-dashboard-forms-and-views.html") Metaplay documentation] to learn more about our generated UI system.

      //- Class selection.
      div(class="tw-mt-6")
        span(class="tw-flex tw-justify-between")
          h4 C# Class
          MInputSingleSelectRadio(
            :model-value="classFilter"
            :options="classFilterOptions"
            size="small"
            @update:model-value="classFilter = $event"
            )
        meta-input-select(
          :value="typeName"
          :options="filteredClassTypeOptions"
          placeholder="Select a class to use..."
          no-clear
          @input="onTypeNameChange"
          )

      //- Layout controls.
      div(class="tw-mt-6 tw-space-y-2")
        h4 Preview Layout Controls
        p(class="tw-text-xs tw-text-neutral-400") Select the layout style and view type to preview the generated form and view components.
        MInputSingleSelectDropdown(
          :model-value="selectedLayout"
          :options="layoutOptions"
          @update:model-value="selectedLayout = $event"
          )
        MInputSingleSelectDropdown(
          :model-value="selectedView"
          :options="viewOptions"
          @update:model-value="selectedView = $event"
          )

  //- Single column layout.
  div(
    v-if="selectedLayout === 'OneColumnLayout'"
    class="tw-space-y-4"
    )
    //- Generated form component.
    MCard(title="Generated Form Preview")
      div(
        v-if="!typeName"
        class="tw-flex tw-justify-center tw-text-neutral-500"
        ) No class selected.
      meta-generated-form(
        v-else
        :key="`form-${key}`"
        :typeName="typeName"
        is-targeting-multiple-players
        :value="formValue"
        @input="formValue = $event"
        )

    //- Generated view card and content component.
    MCard(:title="generatedViewTitle")
      div(
        v-if="!typeName"
        class="tw-flex tw-justify-center tw-text-neutral-500"
        ) No class selected.
      meta-generated-content(
        v-else-if="selectedView === 'MetaGeneratedContent'"
        :key="`content-${key}`"
        :typeName="typeName"
        :value="formValue"
        :title="''"
        )
      meta-generated-card(
        v-else-if="selectedView === 'MetaGeneratedCard'"
        :key="`card-${key}`"
        :typeName="typeName"
        :value="formValue"
        :title="''"
        )
      meta-generated-section(
        v-else-if="selectedView === 'MetaGeneratedSection'"
        :key="`section-${key}`"
        :typeName="typeName"
        :value="formValue"
        :title="''"
        is-targeting-multiple-players
        )

  //- Two column layout.
  div(
    v-else
    class="tw-space-4 tw-my-4"
    )
    MTwoColumnLayout
      //- Generated form component.
      MCard(title="Generated Form Preview")
        div(
          v-if="!typeName"
          class="tw-flex tw-justify-center tw-text-neutral-500"
          ) No class selected.
        meta-generated-form(
          v-else
          :key="key"
          :typeName="typeName"
          is-targeting-multiple-players
          :value="formValue"
          @input="formValue = $event"
          )

      //- Generated view card and content component.
      MCard(:title="generatedViewTitle")
        div(
          v-if="!typeName"
          class="tw-flex tw-justify-center tw-text-neutral-500"
          ) No class selected.
        meta-generated-content(
          v-else-if="selectedView === 'MetaGeneratedContent'"
          :key="`content-${key}`"
          :typeName="typeName"
          :value="formValue"
          :title="''"
          )
        meta-generated-card(
          v-else-if="selectedView === 'MetaGeneratedCard'"
          :key="`card-${key}`"
          :typeName="typeName"
          :value="formValue"
          :title="''"
          )
        meta-generated-section(
          v-else-if="selectedView === 'MetaGeneratedSection'"
          :key="`section-${key}`"
          :typeName="typeName"
          :value="formValue"
          :title="''"
          )

  //- Raw data.
  MCard(
    title="Raw Generated Form Data"
    class="tw-mt-4"
    )
    div(
      v-if="!typeName"
      class="tw-flex tw-justify-center tw-text-neutral-500"
      ) No class selected.
    pre(v-else) {{ formValue }}
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import type { MetaInputSelectOption } from '@metaplay/meta-ui'
import {
  MTextButton,
  MCard,
  MInputSingleSelectRadio,
  MTwoColumnLayout,
  MPageOverviewCard,
  MViewContainer,
  MInputSingleSelectDropdown,
} from '@metaplay/meta-ui-next'
import { makeIntoUniqueKey } from '@metaplay/meta-utilities'
import { fetchSubscriptionDataOnceOnly } from '@metaplay/subscriptions'

import MetaGeneratedCard from '../components/generatedui/components/MetaGeneratedCard.vue'
import MetaGeneratedContent from '../components/generatedui/components/MetaGeneratedContent.vue'
import MetaGeneratedForm from '../components/generatedui/components/MetaGeneratedForm.vue'
import MetaGeneratedSection from '../components/generatedui/components/MetaGeneratedSection.vue'
import { extractSingleValueFromQueryStringOrDefault } from '../coreUtils'
import { getAllGeneratedFormTypes } from '../subscription_options/general'

const route = useRoute()
const router = useRouter()

/**
 * Selected server side class types.
 * @example Metaplay.Server.BroadcastMessageContents
 */
const typeName = ref(extractSingleValueFromQueryStringOrDefault(route.query, 'typeName', ''))

/**
 * A unique identifier used to track component re-renders in the virtual DOM.
 */
const key = makeIntoUniqueKey(typeName.value)

/**
 * Selected generated view. Default is 'MetaGeneratedSection'.
 * @example MetaGeneratedSection
 */
const selectedView = ref(
  extractSingleValueFromQueryStringOrDefault(route.query, 'selectedView', 'MetaGeneratedSection')
)

/**
 * Data to populate the generated form.
 */
const formValue = ref({})

/**
 * Options to select based on server side C# class types.
 */
const allClassTypeOptions = ref<Array<MetaInputSelectOption<string>>>([])

/**
 * Options to select based on sample class types.
 */
const allSampleTypeOptions = ref<Array<MetaInputSelectOption<string>>>([])

function onTypeNameChange(value: string): void {
  typeName.value = value
  formValue.value = {}
}

/**
 * Selected layout style. Default is 'OneColumnLayout'.
 * @example OneColumnLayout
 */
const selectedLayout = ref(extractSingleValueFromQueryStringOrDefault(route.query, 'selectedLayout', 'TwoColumnLayout'))

/**
 * Options to select layout style.
 */
const layoutOptions = [
  { label: 'Single column layout', value: 'OneColumnLayout' },
  { label: 'Two column layout', value: 'TwoColumnLayout' },
]

/**
 * Options to select based on generated UI view types.
 */
const viewOptions = [
  { label: 'MetaGeneratedSection', value: 'MetaGeneratedSection' },
  { label: 'MetaGeneratedContent', value: 'MetaGeneratedContent' },
  { label: 'MetaGeneratedCard', value: 'MetaGeneratedCard' },
]

/**
 * Fetch all generated form types once only.
 */
onMounted(() => {
  fetchSubscriptionDataOnceOnly(getAllGeneratedFormTypes())
    .then((data) => {
      // Populate the select options for server side C# class types.
      allClassTypeOptions.value = data.allTypes
        .map((type: any) => {
          return { id: type, value: type }
        })
        .sort((a: MetaInputSelectOption<string>, b: MetaInputSelectOption<string>) => a.id.localeCompare(b.id))

      // Populate the select options for sample class types.
      allSampleTypeOptions.value = data.exampleTypes
        .map((type: any) => {
          return { id: type, value: type }
        })
        .sort((a: MetaInputSelectOption<string>, b: MetaInputSelectOption<string>) => a.id.localeCompare(b.id))
    })
    .catch((e) => {
      // Error fetching initial data.
      console.log(e)
    })
})

/**
 * List of selectable class filter options.
 */
const classFilterOptions = [
  { label: 'Samples only', value: 'Samples' },
  { label: 'All types', value: 'All' },
]

/*
 * Selected filter type.
 */
const classFilter = ref(extractSingleValueFromQueryStringOrDefault(route.query, 'classFilter', 'Samples'))

/**
 * List of selectable class types based on the selected class filter.
 */
const filteredClassTypeOptions = computed(() => {
  if (classFilter.value === 'Samples') {
    return allSampleTypeOptions.value
  } else {
    return allClassTypeOptions.value
  }
})

/**
 * Title to display on the card based on selected generated view.
 */
const generatedViewTitle = computed(() => {
  if (selectedView.value === 'MetaGeneratedSection') {
    return 'Generated Section Preview'
  } else if (selectedView.value === 'MetaGeneratedContent') {
    return 'Generated Content Preview'
  } else if (selectedView.value === 'MetaGeneratedCard') {
    return 'Generated Card Preview'
  }
  return ''
})

// If any parameter updates...
watch(
  [typeName, classFilter, selectedLayout, selectedView],
  async () => {
    // ..update the URL with the new parameters.
    const params: Record<string, string> = {}

    if (typeName.value) {
      params.typeName = typeName.value
    }

    params.classFilter = classFilter.value

    if (selectedLayout.value) {
      params.selectedLayout = selectedLayout.value
    }

    if (selectedView.value) {
      params.selectedView = selectedView.value
    }

    // Update query params in URL.
    await router.replace({ query: params })
  },
  { deep: true }
)
</script>
