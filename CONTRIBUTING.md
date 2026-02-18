# Contributing to Simcoe-Design

Thank you for your interest in contributing to the Electrical Component Sandbox project!

## Branch Strategy

**The `main` branch is the primary development branch for this repository.**

- All pull requests should target the `main` branch
- Feature branches should be created from `main`
- The `main` branch contains the latest stable development version

## Getting Started

1. Fork the repository
2. Clone your fork:
   ```bash
   git clone https://github.com/YOUR_USERNAME/Simcoe-Design.git
   cd Simcoe-Design
   ```

3. Create a feature branch from `main`:
   ```bash
   git checkout main
   git pull origin main
   git checkout -b feature/your-feature-name
   ```

4. Make your changes and commit them with descriptive messages

5. Push to your fork:
   ```bash
   git push origin feature/your-feature-name
   ```

6. Open a Pull Request against the `main` branch

## Development Guidelines

- Ensure your code builds successfully with `dotnet build`
- Run tests before submitting: `dotnet test`
- Follow the existing code style and conventions
- Update documentation if you change functionality
- Keep commits focused and write clear commit messages

## Pull Request Process

1. Update the README.md or relevant documentation with details of changes if applicable
2. Ensure all tests pass
3. Your PR will be reviewed by maintainers
4. Once approved, your changes will be merged into `main`

## Code of Conduct

Please be respectful and constructive in all interactions with the community.

## Questions?

Feel free to open an issue for questions or discussions about contributions.
