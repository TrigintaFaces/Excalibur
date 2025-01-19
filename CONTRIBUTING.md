# Contributing Guidelines

Thank you for your interest in contributing to the Excalibur Framework! Your contributions are vital to making this framework better and more useful for everyone. Please follow these guidelines to ensure a smooth and productive collaboration.

---

## Code of Conduct

We are committed to fostering an open and inclusive environment. Please adhere to the [Contributor Covenant Code of Conduct](https://www.contributor-covenant.org/version/2/1/code_of_conduct/) when interacting with the community.

---

- ## Table of Contents

- [Contributing Guidelines](#contributing-guidelines)
  - [Code of Conduct](#code-of-conduct)
  - [Development Setup](#development-setup)
    - [Prerequisites](#prerequisites)
  - [How to Contribute](#how-to-contribute)
    - [Reporting Issues](#reporting-issues)
    - [Submitting Code](#submitting-code)
    - [Branch Naming](#branch-naming)
      - [Workflow](#workflow)
      - [Coding Standards](#coding-standards)
      - [Pull Request Guidelines](#pull-request-guidelines)
  - [Feedback and Support](#feedback-and-support)

---

## Development Setup

### Prerequisites

1. Ensure you have the following tools installed:
   - [Git](https://git-scm.com/)
   - [.NET SDK 8.0 or higher](https://dotnet.microsoft.com/)
   - [Visual Studio](https://visualstudio.microsoft.com/) or [Visual Studio Code](https://code.visualstudio.com/) with C# extensions.

2. Fork and clone this repository:

   ```sh
   git clone https://github.com/TrigintaFaces/Excalibur.git
   cd Excalibur
   ```

3. Install dependencies using the .NET CLI:

   ```sh
   dotnet restore
   ```

4. Build and test the project to ensure everything works:

   ```sh
   dotnet build
   dotnet test

---

## How to Contribute

You can contribute in many ways:

1. Reporting bugs and suggesting features via GitHub issues.
2. Submitting pull requests for code contributions.
3. Improving documentation.
4. Testing existing functionality and reporting inconsistencies.

### Reporting Issues

If you encounter a bug or have a feature request, use the appropriate [issue template](.github/ISSUE_TEMPLATE) to submit it:

- **🐛 Bug Report**: For reporting bugs or issues.
- **🚀 Feature Request**: For proposing new features.
- **💬 Question/Discussion**: For inquiries or open discussions.
- **📒 Documentation Request**: For requesting updates to the documentation

### Submitting Code

#### Branch Naming

- **feature/{feature-name}**: For new features.
- **bugfix/{issue-id}**: For fixing bugs.
- **docs/{change-description}**: For documentation changes.

#### Workflow

1. Create a new branch:

   ```sh
   git checkout -b feature/your-feature-name
   ```

2. Make your changes. Follow the coding standards outlined below.

3. Test your changes thoroughly. Ensure unit, integration, and functional tests pass.

4. Commit your changes with a descriptive message:

   ```sh
   git commit -m "Add feature X"
   ```

5. Push your branch:

   ```sh
   git push origin feature/your-feature-name
   ```

6. Submit a pull request (PR) to the main repository. Clearly describe the changes made and link any related issues.

#### Coding Standards

- **Follow C# conventions**: Use [Microsoft\'s C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions).
- **Document code**: Include XML documentation for public methods, classes, and properties.
- **Write tests**: All features and fixes must include relevant unit, functional, and/or integration tests.
- **Coding standards**: Follow the framework's coding standards and style.
- **Update documentation**: Update any relevant documentation.

#### Pull Request Guidelines

- Ensure your PR title is descriptive.
- Reference related issues in the PR description (e.g., "Closes #123").
- Wait for reviews and address any requested changes promptly.

## Feedback and Support

For questions, join the discussion in [GitHub Discussions](https://github.com/<your-repo>/discussions) or file a question issue.

---

Thank you for contributing🎉!
