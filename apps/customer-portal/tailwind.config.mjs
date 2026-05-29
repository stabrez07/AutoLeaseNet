/** @type {import('tailwindcss').Config} */
export default {
  content: ['./app/**/*.{ts,tsx}', './components/**/*.{ts,tsx}', './lib/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        // Same palette as the web-portal so the two surfaces feel like one product.
        brand: {
          50: '#eef6ff',
          100: '#d9e9ff',
          200: '#bcd8ff',
          300: '#8ebcff',
          400: '#5896ff',
          500: '#2f72ff',
          600: '#1854e6',
          700: '#1342b8',
          800: '#143b94',
          900: '#163773',
        },
      },
      fontFamily: {
        sans: ['Inter', 'Noto Kufi Arabic', 'system-ui', '-apple-system', 'Segoe UI', 'sans-serif'],
      },
    },
  },
  plugins: [],
}
