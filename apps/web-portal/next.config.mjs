/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  transpilePackages: ['@autoleasenet/ui', '@autoleasenet/contracts'],
  experimental: {
    typedRoutes: true,
  },
}

export default nextConfig
