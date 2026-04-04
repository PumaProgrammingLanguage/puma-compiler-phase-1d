#include "pch.h"
#include "framework.h"
#include "Charactor.hpp"
#include "String.hpp" // needed for Charactor::ToString()
#include <cstddef>

namespace Puma {
namespace Types
{
	namespace
	{
		constexpr uint8_t UTF8CharSizeLookup[32] =
		{
			1, 1, 1, 1, 1, 1, 1, 1,
			1, 1, 1, 1, 1, 1, 1, 1,
			1, 1, 1, 1, 1, 1, 1, 1,
			2, 2, 2, 2,
			3, 3,
			4,
			1
		};
	}

	Charactor::Charactor() noexcept
		: packedValue(0U)
	{
	}

	Charactor::Charactor(const Charactor& source) noexcept
		: packedValue(source.packedValue)
	{
	}

	Charactor::Charactor(const uint8_t* data) noexcept
	: packedValue(0U)
	{
		if (data == nullptr)
		{
			return;
		}

		const uint8_t charSize = GetCharSize(data[0]); // 1..4

		// Copy up to 4 bytes
		memcpy_s(codeUnits, sizeof(codeUnits), data, charSize);
	}

	Charactor::~Charactor() noexcept = default;

	Charactor& Charactor::operator=(const Charactor& source) noexcept
	{
		if (this != &source)
		{
			packedValue = source.packedValue;
		}
		return *this;
	}

	String Charactor::ToString() const noexcept
	{
		const uint8_t charSize = GetCharSize(codeUnits[0]); // 1..4
		return String(codeUnits, charSize);
	}

	const uint8_t* Charactor::ToUTF8() const noexcept
	{
		return codeUnits;
	}

	const uint8_t Charactor::GetCharSize() const noexcept
	{
		return UTF8CharSizeLookup[codeUnits[0] >> 3];
	}

	const uint8_t Charactor::GetCharSize(const uint8_t c) noexcept
	{
		return UTF8CharSizeLookup[c >> 3];
	}
} // namespace Types
} // namespace Puma