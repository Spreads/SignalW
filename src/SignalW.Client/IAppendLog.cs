using System;
using System.Collections.Generic;
using System.Text;

namespace Spreads.SignalW.Client
{

    public interface IClaim
    {
        void Commit();
        void Abort();
        Memory<byte> Memory { get; }
    }

    public delegate void OnAppendHandler(long streamId, long version);

    public interface IAppendLog<TImpl, TClaim> 
        where TImpl : IAppendLog<TImpl, TClaim> 
        where TClaim : IClaim
    {
        TClaim Claim(long streamId, int length);

        event OnAppendHandler OnAppend;
    }
}
