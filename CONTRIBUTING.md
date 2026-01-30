# Contributing to Herald

Thank you for your interest in contributing to Herald! This document provides guidelines for contributing to the project.

## Getting Started

1. **Fork the repository** on GitHub
2. **Clone your fork** locally:
   ```bash
   git clone https://github.com/YOUR_USERNAME/herald.git
   cd herald
   ```
3. **Set up the development environment**:
   ```bash
   python -m venv venv
   venv\Scripts\activate
   pip install -r requirements.txt
   ```

## Development Workflow

1. **Create a feature branch** from `master`:
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Make your changes** following the code style guidelines below

3. **Test your changes**:
   - Run the application manually: `python src/main.py`
   - Test all relevant features affected by your changes
   - Verify hotkeys work with administrator privileges

4. **Commit your changes** with clear, descriptive messages:
   ```bash
   git commit -m "Add feature: brief description"
   ```

5. **Push to your fork**:
   ```bash
   git push origin feature/your-feature-name
   ```

6. **Create a Pull Request** on GitHub with:
   - Clear description of what your changes do
   - Reference any related issues
   - Screenshots/demo if UI changes

## Code Style Guidelines

- **Python version**: 3.10+
- **Line length**: 100 characters maximum
- **Imports**: Standard library, then third-party, then local
- **Docstrings**: Use for public functions and classes
- **Comments**: Explain "why," not "what"
- **Naming**:
  - `snake_case` for functions and variables
  - `PascalCase` for classes
  - Descriptive names over abbreviations

## Testing

Before submitting a PR, ensure:
- [ ] Application launches without errors
- [ ] All hotkeys work as expected
- [ ] System tray menu functions correctly
- [ ] Settings persist across restarts
- [ ] Both online (edge-tts) and offline (pyttsx3) voices work
- [ ] No console errors or warnings

## Reporting Bugs

Use the [issue tracker](https://github.com/ityeti/herald/issues) to report bugs. Include:

- **OS version** (Windows 10/11)
- **Python version** (`python --version`)
- **Steps to reproduce** the issue
- **Expected behavior**
- **Actual behavior**
- **Logs** from `logs/herald.log` if applicable

## Feature Requests

Feature requests are welcome! Please:
- Check existing issues first to avoid duplicates
- Clearly describe the use case
- Explain how it would benefit users
- Consider implementation complexity

## Pull Request Guidelines

- **One feature per PR** - Keep changes focused
- **Update README** if adding user-facing features
- **Add comments** for complex logic
- **Test thoroughly** before submitting
- **Respond to feedback** - PRs may require revisions

## Areas for Contribution

Looking for ideas? Consider:

- **Testing** - Test on different Windows versions and hardware
- **Documentation** - Improve README, add tutorials
- **Accessibility** - Improve for screen reader users
- **Performance** - Optimize startup time or memory usage
- **Voice options** - Add support for more TTS engines
- **Hotkey improvements** - More customization options
- **UI enhancements** - Settings panel, better tray menu

## Questions?

If you have questions about contributing, feel free to:
- Open an issue for discussion
- Reach out through GitHub Discussions

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

Thank you for helping make Herald better!
