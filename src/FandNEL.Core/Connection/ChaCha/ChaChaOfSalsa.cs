using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

namespace FandNEL.Core.Connection.ChaCha;

public sealed class ChaChaOfSalsa : ChaCha7539Engine
{
	public override string AlgorithmName => $"ChaCha{rounds}";

	public ChaChaOfSalsa(byte[] key, byte[] iv, bool encryption, int rounds = 8)
	{
		base.rounds = rounds;
		Init(encryption, new ParametersWithIV(new KeyParameter(key), iv));
	}
}
