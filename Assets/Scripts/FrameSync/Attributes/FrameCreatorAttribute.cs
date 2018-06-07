using System;

abstract public class FrameClassAttribute : Attribute
{
    abstract public int CreatorID 
    {  
        get;
    }
}