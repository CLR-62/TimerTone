using System;
using System.Dynamic;
using System.Runtime.CompilerServices;

namespace TimerTone.Core;

public static class ProgramStatusHandler
{
    public static event EventHandler<ProgramStatusChangedEventArgs>? OnProgramStatusChanged;
    public static ProgramStatus CurrentProgramStatus { get; private set; } =  ProgramStatus.Loading;
    
    public static void ChangeProgramStatus(ProgramStatus status)
    {
        ProgramStatus prev = status;
        CurrentProgramStatus = status;
        OnProgramStatusChanged?.Invoke(null, new ProgramStatusChangedEventArgs{ ProgramStatus  = status, PrevProgramStatus = prev } );
    }
}

public class ProgramStatusChangedEventArgs : EventArgs
{
    public ProgramStatus PrevProgramStatus { get; set; }
    public ProgramStatus ProgramStatus { get; set; }
}

public enum ProgramStatus
{
    Loading,
    Pending,
    NewProgOpened,
    ProgramAddSequenceAborted,
    ProgramAddSequenceCompleted,
    ErrorPAx0, //NON EXE
    ErrorPAx1, //ERROR DURING CREATING PROGRAM ITEM
    ErrorPAx2, //ALREADY ADDED
    Monitoring
}