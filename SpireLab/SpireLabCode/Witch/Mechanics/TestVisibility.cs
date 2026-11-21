using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SpireLab.Smoke")]

#if DEBUG
[assembly: InternalsVisibleTo("SpireLab.TestLab")]
#endif
