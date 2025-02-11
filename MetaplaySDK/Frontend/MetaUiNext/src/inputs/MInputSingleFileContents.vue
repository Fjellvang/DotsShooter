<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MInputSingleFile(
  :label="label"
  :model-value="selectedFile"
  :validation-function="validationFunction"
  :disabled="disabled"
  :variant="fileError ? 'danger' : variant"
  :hint-message="fileError ?? hintMessage"
  :placeholder="placeholder"
  :accepted-file-types="acceptedFileTypes"
  @update:model-value="onFileSelected"
  )
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'

import type { FileError } from '@zag-js/file-upload'

import MInputSingleFile from './MInputSingleFile.vue'

const props = withDefaults(
  defineProps<{
    /**
     * The value of the input. Can be undefined.
     */
    modelValue?: string
    /**
     * Optional: A function that validates the selected files. Should return an array of errors.
     * @param file The file to validate.
     */
    validationFunction?: (file: File) => FileError[] | null
    /**
     * Optional: Show a label for the input.
     */
    label?: string
    /**
     * Optional: Disable the input. Defaults to false.
     */
    disabled?: boolean
    /**
     * Optional: Visual variant of the input. Defaults to 'default'.
     */
    variant?: 'default' | 'danger' | 'success' | 'loading'
    /**
     * Optional: Hint message to show below the input.
     */
    hintMessage?: string
    /**
     * Optional: Placeholder text to show in the input.
     */
    placeholder?: string
    /**
     * Optional: Limit the file types that can be selected. Defaults to all file types.
     * @example 'image/*' or '.png' or '.png,.jpg,.jpeg'
     */
    acceptedFileTypes?: string | Record<string, string[]>
  }>(),
  {
    modelValue: undefined,
    label: undefined,
    variant: 'default',
    hintMessage: undefined,
    placeholder: undefined,
    acceptedFileTypes: undefined,
    validationFunction: undefined,
  }
)

const emit = defineEmits<{
  'update:modelValue': [value?: string]
}>()

/**
 * The underlying Zag component does not support an initial value. If the component is mounted with an initial value
 * then that value *will not* be shown in the input but it *will* still be available in the model value! This means
 * that the value will be invisible to the user, and the UI and value will differ. To fix this, we explicitly clear the
 * model value when the component is mounted.
 */
onMounted(() => {
  if (props.modelValue) {
    emit('update:modelValue', undefined)
  }
})

const selectedFile = ref<File>()
const fileError = ref<string>()

async function onFileSelected(file?: File): Promise<void> {
  if (!file) {
    selectedFile.value = undefined
    emit('update:modelValue', undefined)
    return
  }

  try {
    const fileContents = await file.text()
    selectedFile.value = file
    emit('update:modelValue', fileContents)
  } catch (error) {
    fileError.value = 'Failed to parse file contents.'
  }
}
</script>
