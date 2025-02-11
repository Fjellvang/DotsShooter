<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer
  template(#overview)
    MPageOverviewCard(title="")
      div(class="tw-p-3 tw-text-center")
        img(:src="ImgUrl")
        div(class="tw-mt-3 tw-text-3xl tw-font-bold tw-uppercase") The computer says no

      p(class="tw-mb-0 tw-mt-4") Page not found!
      span(class="tw-text-sm tw-text-neutral-400") Maybe check the URL? Do let us know if you think this a bug 🤔
</template>

<script lang="ts" setup>
// This path gets evaluated after TS build, so the image import path needs to work from inside /dist.
import { MCard, MPageOverviewCard, MViewContainer } from '@metaplay/meta-ui-next'

import ImgUrl from '../../src/assets/old-computer.png'
</script>
