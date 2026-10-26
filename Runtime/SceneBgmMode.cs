namespace Kagami.SceneBgm
{
    /// <summary>
    /// シーンに入ったときのBGMの扱い。
    /// <para>
    /// 値は設定アセットに int で保存されるため、<b>既存の値は変更・削除しないこと</b>（追加のみ可）。
    /// </para>
    /// </summary>
    public enum SceneBgmMode
    {
        /// <summary>未設定。ビルド時に警告を出す。実行時は直前のBGMを継続する。</summary>
        Unset = 0,

        /// <summary>直前のBGMをそのまま継続する。</summary>
        Continue = 1,

        /// <summary>フェードアウトして停止する。</summary>
        Stop = 2,

        /// <summary>指定したクリップへクロスフェードする（同じクリップが再生中なら何もしない）。</summary>
        Clip = 3,
    }
}
