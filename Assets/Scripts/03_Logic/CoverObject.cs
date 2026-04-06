using UnityEngine;

public class CoverObject : MonoBehaviour
{
    public CoverType CoverType = CoverType.Full;
}

public enum CoverType
{
    Full,
    Partial
}

public enum CoverArchetype
{
    Pillar,
    Barrier,
    LBlock
}