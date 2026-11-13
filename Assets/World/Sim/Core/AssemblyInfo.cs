using System.Runtime.CompilerServices;

// The EditMode tests (and the dotnet harness) check logistics internals (routes, demand) directly.
[assembly: InternalsVisibleTo("World.Sim.Tests")]
