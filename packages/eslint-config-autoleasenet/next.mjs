import base from './index.mjs'

/** @type {import("eslint").Linter.Config} */
export default {
  ...base,
  extends: [...(base.extends ?? []), 'next/core-web-vitals'],
}
