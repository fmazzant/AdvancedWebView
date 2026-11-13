# @mafesoft/advancedwebview-types

TypeScript typings for the JavaScript bridge of [AdvancedWebView](https://github.com/fmazzant/AdvancedWebView), the .NET MAUI `WebView` ([Mafesoft.Maui.AdvancedWebView](https://www.nuget.org/packages/Mafesoft.Maui.AdvancedWebView) on NuGet). They describe the `window.AdvancedWebView` object that the app injects into every page: `ready`, `postMessage`, `addEventListener`, `removeEventListener`, `invoke<T>` and `retry`.

The package contains only type declarations: the bridge itself is injected by the app, so there is nothing to import at run time.

## Installation

```shell
npm install --save-dev @mafesoft/advancedwebview-types
```

Then add the package to `types` in `tsconfig.json`:

```json
{
  "compilerOptions": {
    "types": ["@mafesoft/advancedwebview-types"]
  }
}
```

or reference it from one file of the project:

```ts
/// <reference types="@mafesoft/advancedwebview-types" />
```

Without npm, copy [`advancedwebview.d.ts`](https://github.com/fmazzant/AdvancedWebView/blob/main/typescript/advancedwebview.d.ts) into the project.

## Usage

```ts
interface User { id: number; name: string; }

await AdvancedWebView.ready;

AdvancedWebView.postMessage({ type: "checkout", total: 42.5 });
AdvancedWebView.addEventListener<{ count: number }>("message", e => console.log(e.data.count));

try {
  const user = await AdvancedWebView.invoke<User>("getUser", { id: 1 });
  console.log(user.name);
} catch (e) {
  const err = e as AdvancedWebViewError;
  console.error(err.code, err.message); // "NotFound", "User 1 does not exist."
}
```

`invoke<T>` and the message listeners take the type of the value the app sends; the typings cannot check it against the C# side.

`AdvancedWebView` is not defined when the page runs outside the app, for example in a desktop browser. Pages that also run there should check `typeof AdvancedWebView !== "undefined"` first.

See the [JavaScript bridge](https://github.com/fmazzant/AdvancedWebView#javascript-bridge) section of the AdvancedWebView README for the app side.

## License

[MIT](https://github.com/fmazzant/AdvancedWebView/blob/main/LICENSE)
