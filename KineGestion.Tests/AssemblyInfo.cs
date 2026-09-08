using Xunit;

// Los tests comparten QueryCache (singleton estático). ClearAll() en constructores de
// otras clases puede corromper la ejecución de los tests de concurrencia si corren en
// paralelo. Desactivar la paralelización de collections evita la carrera.
[assembly: CollectionBehavior(DisableTestParallelization = true)]