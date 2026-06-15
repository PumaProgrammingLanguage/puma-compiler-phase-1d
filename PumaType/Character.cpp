#include "Character.hpp"
#include "String.hpp" // needed for Character::ToString()
#include <cstddef>
#include <cstring>

namespace Puma {
namespace Type
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

    Character::Character() noexcept
        : codePoint(0U)
    {
    }

    Character::Character(const Character& source) noexcept
        : codePoint(source.codePoint)
    {
    }

    Character::Character(const uint8_t* utf8) noexcept
    : codePoint(0U)
    {
        if (utf8 == nullptr)
        {
            return;
        }

        const uint8_t charSize = GetCharSize(utf8[0]); // 1..4

        // Copy up to 4 bytes
        std::memcpy(codeUnits, utf8, charSize);
    }

    Character::~Character() noexcept = default;

    Character& Character::operator=(const Character& source) noexcept
    {
        if (this != &source)
        {
            codePoint = source.codePoint;
        }
        return *this;
    }

    // Less-than operator for ordering (e.g., for sorting)
    bool operator<(const Character& other) const noexcept
	{
	    // Compare whole character
		return codePoint < other.codePoint;
	}
    // Greater-than operator for ordering
    bool operator>(const Character& other) const noexcept
    {
		return codePoint > other.codePoint;
    }
    // Equality
    bool operator==(const Character& other) const noexcept
    {
        return codePoint == other.codePoint;
    }
    // Inequality
    bool operator!=(const Character& other) const noexcept
    {
        return codePoint != other.codePoint;
    }
    // Less-than-or-equal operator for ordering
    bool operator<=(const Character& other) const noexcept
    {
        return codePoint <= other.codePoint;
    }
    // Greater-than-or-equal operator for ordering
    bool operator>=(const Character& other) const noexcept
    {
        return codePoint >= other.codePoint;
    }

    String Character::ToString() const noexcept
    {
        const uint8_t charSize = GetCharSize(codeUnits[0]); // 1..4
        return String(&codeUnits[0], (uint32_t)charSize);
    }

    const uint8_t* Character::ToUTF8() const noexcept
    {
        return codeUnits;
    }

    const uint8_t Character::GetCharSize() const noexcept
    {
        return UTF8CharSizeLookup[codeUnits[0] >> 3];
    }

    const uint8_t Character::GetCharSize(const uint8_t firstCodeUnit) noexcept
    {
        return UTF8CharSizeLookup[firstCodeUnit >> 3];
    }
} // namespace Type
} // namespace Puma
