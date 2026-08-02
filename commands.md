# NESTED KWIN
scripts/publish-shell.sh --fast
scripts/run-kwin-prototype.sh


# PRODUCTION / ISO PUBLISH
scripts/publish-shell.sh

# SHELL
DAISYOS_SHELL_SERVICES=real dotnet run --project src/DaisyOS.Shell -- --skip-onboarding
