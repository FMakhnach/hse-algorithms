using System;

namespace RoaringBitmap_InvisibleJoin.Bitmaps
{
    /// <summary>
    /// Fake full bitmap especially for the case of empty input.
    /// </summary>
    public class FakeFullBitmap : Bitmap
    {
        public override void And(Bitmap other)
            => throw new NotSupportedException();

        public override bool Get(int i) => true;

        public override void Set(int i, bool value)
            => throw new NotSupportedException();
    }
}
