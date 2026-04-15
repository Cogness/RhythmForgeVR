namespace RhythmForge.Core.Sequencing
{
    public struct ShapeRole
    {
        public int index;
        public int count;
        public bool IsPrimary => index == 0;

        public static ShapeRole Primary => new ShapeRole { index = 0, count = 1 };
    }

    /// <summary>
    /// Static bridge that exposes the role (ensemble position) of the shape currently being
    /// derived, without changing deriver interfaces. Role index 0 is the primary voice; 1, 2, ...
    /// are counter / pedal / fill voices. Derivers adapt their output to avoid stepping on the
    /// primary when multiple shapes of the same mode coexist.
    ///
    /// Set by DraftBuilder and SessionStore re-derivation paths before each Derive() call on
    /// the Unity main thread. Derivers read via Current.
    /// </summary>
    public static class ShapeRoleProvider
    {
        [System.ThreadStatic]
        private static ShapeRole _current = ShapeRole.Primary;

        public static void Set(ShapeRole role) => _current = role;

        public static void Clear() => _current = ShapeRole.Primary;

        public static ShapeRole Current => _current.count > 0 ? _current : ShapeRole.Primary;
    }
}
