using System;

abstract public class TurnClassAttribute : Attribute
{
    abstract public int CreatorID 
    {  
        get;
    }
}