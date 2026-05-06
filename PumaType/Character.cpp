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
        : packedValue(0U)
    {
    }

    Character::Character(const Character& source) noexcept
        : packedValue(source.packedValue)
    {
    }

    Character::Character(const uint8_t* utf8) noexcept
    : packedValue(0U)
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
            packedValue = source.packedValue;
        }
        return *this;
    }

    String Character::ToString() const noexcept
    {
        const uint8_t charSize = GetCharSize(codeUnits[0]); // 1..4
        return String(codeUnits, charSize);
    }

    const uint8_t* Character::ToUTF8() const noexcept
    {
        return codeUnits;
    }

    const uint8_t Character::GetCharSize() const noexcept
    {
        return UTF8CharSizeLookup[codeUnits[0] >> 3];
    }

    const uint8_t Character::GetCharSize(const uint8_t c) noexcept
    {
        return UTF8CharSizeLookup[c >> 3];
    }
} // namespace Type
} // namespace Puma
