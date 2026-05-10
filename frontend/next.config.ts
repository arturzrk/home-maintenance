import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // The backend API URL is injected via environment variables.
  // During local dev this defaults to http://localhost:5000.
  // In production it should be set via NEXT_PUBLIC_API_URL.
  env: {
    NEXT_PUBLIC_API_URL:
      process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000",
  },
};

export default nextConfig;
