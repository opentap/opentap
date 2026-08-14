# OpenTAP Brew Formula
This folder contains the Homebrew formula for installing OpenTAP on macOS.

# Installation
To install OpenTAP using Homebrew, run the following command:
```bash
brew install opentap
```

# Development
...

## Prerequisites
Set `HOMEBREW_NO_INSTALL_FROM_API=1` to force brew to use the local repository instead of the API.

Update brew:
```bash
brew update
```

Clone homebrew-core repository:
```bash
`brew tap --force homebrew/core`
```

## Installing the formula
If you made changes to the formula, you can install it from a local file by running either of the following commands:

**Install from a local file:**
```bash
HOMEBREW_NO_INSTALL_FROM_API=1 brew install opentap --verbose
```

**Install from a local file in interactive mode:**
```bash
HOMEBREW_NO_INSTALL_FROM_API=1 brew install opentap --interactive
```

**Reinstall from a local file:**
```bash
HOMEBREW_NO_INSTALL_FROM_API=1 brew reinstall opentap --verbose
```

**Uninstall the formula:**
```bash
HOMEBREW_NO_INSTALL_FROM_API=1 brew uninstall opentap --verbose
```

## Running the formula test
Before submitting the formula, run the test to check for any issues:
```bash
HOMEBREW_NO_INSTALL_FROM_API=1 brew test opentap
```

## Running the formula audit
Before submitting the formula, run the audit to check for any issues:
```bash
HOMEBREW_NO_INSTALL_FROM_API=1 brew audit --new --formula opentap
```

# Updating the formula
...





# Resources
- [Homebrew Formula Cookbook](https://docs.brew.sh/Formula-Cookbook)
- [Acceptable Formulae](https://docs.brew.sh/Acceptable-Formulae)
- [Formula API](https://rubydoc.brew.sh/Formula.html)
- [FAQ](https://docs.brew.sh/FAQ)
