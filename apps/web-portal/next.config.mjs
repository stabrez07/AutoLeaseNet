/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  transpilePackages: ['@autoleasenet/ui', '@autoleasenet/contracts'],
  experimental: {
    typedRoutes: false,
  },
}

export default nextConfig
