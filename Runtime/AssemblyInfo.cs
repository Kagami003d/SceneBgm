using System.Runtime.CompilerServices;

// 内部APIはエディタ拡張とテストからのみ使う（公開APIは増やさない）。
[assembly: InternalsVisibleTo("Kagami.SceneBgm.Editor")]
[assembly: InternalsVisibleTo("Kagami.SceneBgm.Tests")]
[assembly: InternalsVisibleTo("Kagami.SceneBgm.Editor.Tests")]
