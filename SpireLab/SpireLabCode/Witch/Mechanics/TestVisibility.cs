using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SpireLab.Smoke")]

#if SPIRELAB_TEST_LAB
[assembly: InternalsVisibleTo("SpireLab.TestLab")]
#endif
