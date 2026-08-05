interface GoogleIconProps {
  className?: string;
}

export function GoogleIcon({ className }: GoogleIconProps) {
  return (
    <svg
      viewBox="0 0 24 24"
      aria-hidden="true"
      className={className}
    >
      <path fill="#4285F4" d="M21.6 12.23c0-.74-.07-1.45-.19-2.14H12v4.05h5.38a4.6 4.6 0 0 1-1.99 3.02v2.63h3.23c1.89-1.74 2.98-4.3 2.98-7.56Z" />
      <path fill="#34A853" d="M12 22c2.7 0 4.97-.9 6.62-2.42l-3.23-2.63c-.9.6-2.04.96-3.39.96-2.6 0-4.8-1.76-5.59-4.12H3.08v2.71A10 10 0 0 0 12 22Z" />
      <path fill="#FBBC05" d="M6.41 13.79A6.02 6.02 0 0 1 6.1 12c0-.62.11-1.22.31-1.79V7.5H3.08A10 10 0 0 0 2 12c0 1.61.39 3.14 1.08 4.5l3.33-2.71Z" />
      <path fill="#EA4335" d="M12 6.09c1.47 0 2.79.5 3.83 1.5l2.87-2.87A9.64 9.64 0 0 0 12 2a10 10 0 0 0-8.92 5.5l3.33 2.71C7.2 7.85 9.4 6.09 12 6.09Z" />
    </svg>
  );
}
