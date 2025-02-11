# Meta Utilities

This is a module of TypeScript utility functions for use in Metaplay SDK related development.

All functions in this module will be well documented with high testing coverage.

## Usage

First import the function from the module and then use it:

```typescript
import { toOrdinalString } from '@metaplay/meta-utilities'

console.log(`Metaplay is ${toOrdinalString(1)}!`)
```

## Contributing to this Module

### Before Adding New Functions

When deciding whether to add a new function to this library, the key considerations are:

- Does the function depend on other Metaplay modules?
- Will the function only be used by a single other module?

If the answer to either of these questions is "yes" then the function should not be added here, instead it should be added to the relevant module directly.

### Adding New Functions

When adding new functions to this module, please remember to:

- Include complete documentation. This means adding `@param`, `@returns`, `@throws` (if appropriate) and at least one `@example` tag.
- We aim for high test coverage in this module so add tests for all `export`ed function. Internal functions do not need to have tests specifically written but it's still recommended to do so.
