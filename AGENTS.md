# Hatband Guidelines

## Organization and Style

- Keep responsibilities cohesive and dependencies explicit, following the separation between the UI, Core, integrations, and infrastructure.
- Prefer methods with clear control flow and names that express intent. Use expression bodies only for genuinely simple expressions, such as computed properties or single-expression methods; do not compress logic involving validation, side effects, or multiple decisions.
- Use guard clauses when they make the flow easier to follow. Do not introduce behaviorless abstractions merely to add layers or satisfy mechanical rules.
- Keep one main structure per file. Small, inseparable types may share a file when that improves readability.
- Make the namespace reflect the file's location within the project.
- Avoid fully qualified type names in code. When names collide or are ambiguous, use a single `using` type alias and refer to the type through that alias.

## Types, Nullability, and Errors

- Keep nullable reference types enabled. Use nullable types (`string?`, `int?`, `Guid?`, etc.) only when absence is an intentional part of the contract; declare required values as required and validate them at the appropriate boundary.
- Do not use sentinel values to represent absence. Do not add `!` or casts that hide nullability that should be addressed by the contract or validation.
- Do not mask problems with defensive programming: avoid arbitrary fallbacks such as `?? ""`, `?? []`, or default values that make an invalid state appear valid. When a required precondition is not met, fail explicitly at the point responsible for enforcing it.
- Use default values only when they are a deliberate contract or domain rule.
- Keep input validation at the boundary and business invariants in the layer responsible for them; do not spread the same rules across ViewModels, use cases, and infrastructure.

## Build and Tests

- Always run .NET builds with elevated command execution (`sandbox_permissions: require_escalated`). Builds in the default sandbox can stall during restore, so do not retry them there. Start with normal parallelism: `dotnet build source/Hatband.slnx`.
- Do not add `-m:1` to builds by default; normal parallel builds are faster. Use `-m:1` only if an elevated build with normal parallelism fails due to resource contention.
- Run tests only when requested, using the corresponding solution or project file and the same escalated execution mode if the default sandbox stalls.
- When changing a solution or project, use the corresponding `.slnx` or `.csproj` file in commands.
- Preserve existing local changes and keep unrelated changes out of the task.
