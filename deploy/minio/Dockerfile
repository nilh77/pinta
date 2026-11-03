# MinIO for the Demo environment on Railway (docs/deploy-railway.md). Same image as the AppHost; the image has no shell,
# so the server arguments live here instead of in Railway's start command.
FROM cgr.dev/chainguard/minio:latest
EXPOSE 9000
CMD ["server", "/data", "--address", ":9000"]
