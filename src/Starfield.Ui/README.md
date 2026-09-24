# Starfield.Ui

The desktop window, built with Avalonia. It is a thin shell over
[`Starfield.App`](../Starfield.App/README.md): the XAML binds to the view models there, and the
code-behind holds only what needs the platform, which is the save dialog and loading PNGs into the
backdrop and the preview.

## Running it

```sh
dotnet run --project src/Starfield.Ui
```

Choose where to save, optionally a width, height and seed, and press Render. The image is rendered in
this process by the same library the command line uses. The preview area shows a loader while it
runs, then the finished picture, with where it was saved as a caption. Blank fields use the defaults,
a 4K image from seed 1, shown greyed inside each field. The Random seed button picks a new sky. The
left and right edges always join. Tile vertically, ticked by default, makes the top and bottom join
too, for a texture that repeats both ways; untick it for a plain top and bottom.

By default each seed picks one of the built-in nebula palettes. Tick Custom nebula colours to choose a
rim colour and a core colour instead; the swatch beside the pickers shows the ramp the clouds will be
painted with, and every seed then keeps that colour and varies only the shapes.

## The look

The window is always dark. Behind the cards is a real star field, rendered with a random seed each time
the window opens, dimmed so the controls stay legible. The cards are translucent with rounded corners,
and a violet accent marks the one button that matters.

## File map

| File | Purpose |
| --- | --- |
| `Program.cs` | Entry point and Avalonia configuration. |
| `App.axaml`, `App.axaml.cs` | Loads the Fluent theme in its dark variant and builds the `MainViewModel`. |
| `MainWindow.axaml` | The styles and layout: a settings bar across the top, the preview filling the rest. |
| `MainWindow.axaml.cs` | The save dialog, loading the backdrop and preview bitmaps, and painting the ramp swatch. |
| `Converters/HexColorConverter.cs` | Binds the pickers' colours to the form's hex text. |
