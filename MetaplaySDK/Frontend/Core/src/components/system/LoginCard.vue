<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(class="tw-container tw-mx-auto tw-h-dvh tw-space-y-3")
  MCard(
    id="system-login-card"
    title="🔑 Authentication Needed"
    )
    p You have accessed the LiveOps Dashboard without logging in first. In cloud deployments we recommend forcing authentication during cluster ingress.
    template(#buttons)
      MButton(@click="login") Login
</template>

<script lang="ts" setup>
import { login } from '@metaplay/game-server-api'
import { MButton, MCard } from '@metaplay/meta-ui-next'
</script>
