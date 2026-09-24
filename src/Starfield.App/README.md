# Starfield.App

The framework-neutral half of the desktop front end: everything the window does that is not drawing.
It references [`Starfield.Core`](../Starfield.Core/README.md) and renders in this process; nothing is
launched. It holds no dependency on any UI toolkit, which is what lets every behaviour here be tested
as plain objects.

## File map

| Area | Types | Purpose |
| --- | --- | --- |
| `ViewModels/RenderRequestViewModel.cs` | `RenderRequestViewModel`, `RequestDefaults`, `RequestBuildResult` | The form: output path, width, height and seed, and `Build()` which turns it into `StarfieldOptions` or a list of problems. |
| `ViewModels/MainViewModel.cs` | `MainViewModel` | The window state: the form, the render in progress, the finished image's path, what the preview area shows, and the backdrop. |
| `Rendering/` | `IRenderService`, `StarfieldRenderService`, `Backdrop` | Rendering on a worker thread with the library, to a file or to memory, with progress forwarded to the window. |
| `Mvvm/` | `ObservableObject`, `RelayCommand`, `AsyncRelayCommand` | The few binding primitives the view models need. |

## How a click becomes an image

1. `RenderRequestViewModel.Build()` reads the form, collects every problem it finds, and otherwise
   lays the size and seed over the built-in preset.
2. `StarfieldRenderService` runs `StarfieldRenderer` on a worker thread while the preview area shows an
   indeterminate loader. The band loop reports progress only once per batch of bands, which is too
   coarse to animate, so the window does not use it.
3. On success `MainViewModel.PreviewPath` points at the file, and the window loads it into the preview
   with the saved path as a caption. Problems, cancellation and failures show in the preview area too.

## The backdrop

When the window opens, `MainViewModel.LoadBackdropAsync` renders a screen-sized sky with a random seed
into memory through the same service, and the window paints it behind the cards. `Backdrop` holds the
options for it: the built-in look, not tiling, dimmed a little so the controls stay legible.
