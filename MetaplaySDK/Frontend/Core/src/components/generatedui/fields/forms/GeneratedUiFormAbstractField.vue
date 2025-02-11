<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(v-if="typeOptions && typeOptions.length === 0") No types available.

template(v-else)
  div(
    v-if="showPicker"
    class="tw-mb-2"
    )
    LabelWithLogicVersion(
      :fieldInfo="abstractFieldInfo"
      :isTargetingMultiplePlayers="isTargetingMultiplePlayers"
      ) {{ displayName }} Type

    meta-input-select(
      :value="selectedType"
      :options="typeOptions"
      no-clear
      searchable
      :disabled="disabled"
      @input="updateSelectedType"
      :data-testid="dataTestid + '-type-input'"
      )
      //- TODO: This is a hack to get the type name to show up in the dropdown. We should migrate to a better solution.
      template(#option="{ option }")
        div {{ option?.typeName.split('.').at(-1) }}
      template(#selectedOption)
        div {{ selectedType?.typeName.split('.').at(-1) }}

    MCallout(
      v-if="typeHasPotentialLogicVersionMismatch"
      title="Potential Logic Version Mismatch"
      variant="danger"
      class="tw-mt-3"
      ) {{ logicVersionMismatchDescription }}

  generated-ui-form-dynamic-component(
    v-if="selectedTypeName"
    :key="selectedTypeName"
    v-bind="props"
    :fieldInfo="{ ...fieldInfo, fieldType: selectedTypeName }"
    :value="value"
    :logic-version="logicVersion"
    :disabled="disabled"
    :isTargetingMultiplePlayers="isTargetingMultiplePlayers"
    @input="update"
    )
</template>

<script lang="ts" setup>
import { computed, onMounted, ref } from 'vue'

import type { MetaInputSelectOption } from '@metaplay/meta-ui'
import { MCallout } from '@metaplay/meta-ui-next'

import LabelWithLogicVersion from '../../components/LabelWithLogicVersion.vue'
import { generatedUiFieldFormEmits, generatedUiFieldFormProps, useGeneratedUiFieldForm } from '../../generatedFieldBase'
import {
  EGeneratedUiTypeKind,
  type IGeneratedUiFieldInfo,
  type IGeneratedUiFieldSchemaDerivedTypeInfo,
} from '../../generatedUiTypes'
import { hasPotentialLogicVersionMismatch, isVersionInRange } from '../../generatedUiUtils'
import GeneratedUiFormDynamicComponent from './GeneratedUiFormDynamicComponent.vue'

const props = defineProps({
  ...generatedUiFieldFormProps,
  value: {
    type: Object,
    default: () => ({}),
  },
})

const emit = defineEmits(generatedUiFieldFormEmits)

const { dataTestid, displayName, update: emitUpdate } = useGeneratedUiFieldForm(props, emit)

// --- Cache values for each type so form is not cleared every time ---

const typeCache: Record<string, any> = ref({})

onMounted(() => {
  if ('$type' in props.value) {
    typeCache.value = {
      ...typeCache.value,
      [props.value.$type]: props.value,
    }
  }
})

// --- Type options ---

function includeTypeOption(type: IGeneratedUiFieldSchemaDerivedTypeInfo): boolean {
  if (type.jsonType in typeCache.value) {
    return true
  }

  if (props.fieldInfo.excludedAbstractTypes?.includes(type.typeName)) {
    return false
  }

  const customFilter = props.abstractTypeFilter?.(props.fieldSchema.typeName)
  if (customFilter) {
    return customFilter(type)
  }
  return !type.isDeprecated
}

const abstractFieldInfo = computed<IGeneratedUiFieldInfo>(() => {
  const abstractFieldInfo = { ...props.fieldInfo }
  // abstractFieldInfo.fieldName = props.
  abstractFieldInfo.addedInVersion = selectedType.value?.addedInVersion
  abstractFieldInfo.removedInVersion = undefined
  abstractFieldInfo.fieldType = selectedType.value?.typeName ?? ''
  abstractFieldInfo.typeKind = EGeneratedUiTypeKind.Class
  return abstractFieldInfo
})

const typeOptions = computed((): Array<MetaInputSelectOption<IGeneratedUiFieldSchemaDerivedTypeInfo>> => {
  if (!props.fieldSchema.derived) {
    throw new Error('Derived schema not defined')
  }
  return props.fieldSchema.derived.filter(includeTypeOption).map((type: IGeneratedUiFieldSchemaDerivedTypeInfo) => {
    return {
      value: type,
      id: (type.isDeprecated ? '(deprecated) ' : '') + type.typeName.split('.').at(-1),
      disabled: !isVersionInRange(type, props.logicVersion),
    }
  })
})

const selectedType = computed(() => {
  if ('$type' in props.value) {
    if (!props.fieldSchema.derived) {
      throw new Error('Derived schema not defined')
    }
    return props.fieldSchema.derived.find((x: any) => x.jsonType === props.value.$type)
  } else {
    return typeOptions.value[0]?.value ?? ''
  }
})

const selectedTypeName = computed(() => {
  return selectedType.value?.typeName
})

const typeHasPotentialLogicVersionMismatch = computed(() => {
  if (selectedType.value === undefined) return false
  // If we are not targetting multiple players, we either disable components based on the player's logic version or we are not targeting players (meaning no warnings are necessary).
  if (!props.isTargetingMultiplePlayers) return false
  return hasPotentialLogicVersionMismatch(selectedType.value)
})

const logicVersionMismatchDescription = computed(() => {
  if (selectedType.value?.addedInVersion !== undefined) {
    return `${selectedType.value.typeName} is only supported in logic versions ${selectedType.value.addedInVersion} and higher. While the server supports a larger range, a subsection of your users might not support this feature.`
  }

  return ``
})

function updateSelectedType(newValue: IGeneratedUiFieldSchemaDerivedTypeInfo): void {
  const newType: string = newValue.jsonType
  if (newType in typeCache.value) {
    emitUpdate(typeCache.value[newType])
  } else {
    emitUpdate({ $type: newType })
  }
}

const showPicker = computed(() => {
  return typeOptions.value.length > 1
})

function update(newValue: any): void {
  const type = selectedType.value?.jsonType
  if (!type) throw new Error('Type not defined')
  const val = { ...newValue, $type: type }

  typeCache.value = {
    ...typeCache.value,
    [type]: val,
  }
  emitUpdate(val)
}
</script>
