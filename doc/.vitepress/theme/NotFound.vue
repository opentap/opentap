<template>
  <Layout/>
</template>

<script setup lang="ts">
import { useData } from 'vitepress'
import DefaultTheme from 'vitepress/theme'
import { useRouter } from 'vitepress'
const { Layout } = DefaultTheme
const { route, go } = useRouter()
const { hash } = useData()

if (route.path.toLowerCase().endsWith(".html") == false) {
  let fixedPathname = route.path;

  if (route.path.endsWith('/') == false)
    fixedPathname += '/';

  fixedPathname += 'Readme.html' + hash.value;

  go(fixedPathname);
}
</script>