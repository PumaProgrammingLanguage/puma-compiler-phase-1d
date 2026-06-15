#ifndef PUMA_TYPE_STRINGITERATOR_HPP
#define PUMA_TYPE_STRINGITERATOR_HPP

#pragma once

#include "Charactor.hpp"
#include <cstdint>

using namespace std;

namespace Puma {
namespace Type
{
    struct StringIterator
    {
    public:
        // Constructors
        StringIterator() noexcept;
        StringIterator(const uint8_t* current, const uint8_t* limit) noexcept;
        StringIterator(const StringIterator& other) noexcept;
        StringIterator(const String& str) noexcept;

        // Assignment
        StringIterator& operator=(const StringIterator& other) noexcept;
        StringIterator& operator=(const uint8_t* ptr) noexcept;

        // Dereference - returns current UTF-8 code unit pointer
        const Charactor operator*() const noexcept;

        // Add raw byte offset (no UTF-8 awareness, just pointer math)
        StringIterator operator+(std::uint32_t offset) const noexcept;

        // Prefix increment / decrement - move by one UTF-8 character
        StringIterator& operator++() noexcept;
        StringIterator& operator--() noexcept;

        // Comparison operators
        bool operator==(const StringIterator& other) const noexcept;
        bool operator!=(const StringIterator& other) const noexcept;

        // Check validity
        bool IsValid() const noexcept;

    private:
        const uint8_t* _current;
        const uint8_t* _limit;
    };

} // namespace Type
} // namespace Puma

#endif // PUMA_TYPE_STRINGITERATOR_HPP
