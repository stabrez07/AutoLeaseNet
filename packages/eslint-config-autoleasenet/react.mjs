import base from './index.mjs'

/** @type {import("eslint").Linter.Config} */
export default {
  ...base,
  extends: [
    ...(base.extends ?? []),
    'plugin:react/recommended',
    'plugin:react-hooks/recommended',
    'plugin:react/jsx-runtime',
  ],
  settings: {
    react: { version: 'detect' },
  },
}
