#ifndef PUMA_TYPE_CHARACTER_HPP
#define PUMA_TYPE_CHARACTER_HPP

#pragma once

#include <cstdint>

namespace Puma {
namespace Type
{
    // Forward declaration to avoid circular include with String.hpp
    union String;

    // Represents a UTF-8 character (code point) as a sequence of up to 4 bytes.
    union Character
    {
    public:
        // Lifetime
        Character() noexcept;
        Character(const Character& source) noexcept;
        Character(const uint8_t* utf8) noexcept;
        ~Character() noexcept;

        // Assignment
        Character& operator=(const Character& source) noexcept;

        // Convert this UTF-8 character into a Puma String.
        String ToString() const noexcept;

        // Get pointer to the UTF-8 code unit.
        const uint8_t* ToUTF8() const noexcept;

        // Returns the number of bytes in the UTF‑8 code unit sequence stored in this Character.
        const uint8_t GetCharSize() const noexcept;

        // Returns the number of bytes in the UTF‑8 code unit sequence starting with 'c'.
        // Invalid leading bytes and continuation bytes return 1.
        static const uint8_t GetCharSize(const uint8_t c)  noexcept;

    private:
        // Raw 4-byte representation (e.g., UTF-8 bytes)
        uint8_t  codeUnits[4];
        // Packed 32-bit representation of the same 4 bytes
        uint32_t packedValue;
    };
} // namespace Type
} // namespace Puma

#endif // PUMA_TYPE_CHARACTER_HPP
