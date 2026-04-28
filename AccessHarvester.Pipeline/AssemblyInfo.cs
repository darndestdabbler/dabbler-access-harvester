using System.Runtime.CompilerServices;

// InternalsVisibleTo for the test assembly so unit tests can exercise
// internal helper methods (e.g., FunctionClassificationBuilder
// .CategorizeTypeLibraryOwner) without exposing them on the public
// surface or duplicating the logic in tests.
[assembly: InternalsVisibleTo("AccessHarvester.Pipeline.Tests")]
