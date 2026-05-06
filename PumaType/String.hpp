#ifndef PUMA_TYPE_STRING_HPP
#define PUMA_TYPE_STRING_HPP

#pragma once

#include "Character.hpp"
#include "StringIterator.hpp"
#include <cstdint>
#include <cstddef>
#include <cstring>
#include <new>

using namespace std;

namespace Puma {
namespace Type
{
#pragma pack(push, 1)
    union String
    {
    public:
		// Constructors
		// Default constructor - creates an empty string (short string with length 0).
        String() noexcept;
		// Copy constructor - creates a new String with the same content as the source. Can transfer ownership.
        String(String& source, bool moveOwner = false) noexcept;
		// Constructs a String from the raw UTF‑8 bytes and explicit length in bytes.
        String(const uint8_t* utf8, uint32_t dataSize) noexcept;
		// Constructs a String from the raw UTF‑8 bytes and explicit length in bytes.
        String(const char* utf8, size_t dataSize) noexcept;
		// Destructor - releases owned resources if this String is an owner of long string data.
        ~String() noexcept;

		// Assignment - does not transfer ownership.
        String& operator=(const String& source) noexcept;
		// Get a copy of this string (copy constructor semantics).
        String ToString() noexcept;
        // Returns a pointer to a null terminated copy of the string.
        // The caller is responsible for deleting the returned buffer.
        const uint8_t* ToUTF8() const noexcept;

        // Set ownership of the string data (for long strings) - 
        // if true, the String will manage memory and free it on destruction; 
        // if false, it will not free memory (caller must manage lifetime).
		void SetOwner() noexcept;
		// Set the string as a borrower (non-owner) - only applicable for long strings; 
        // short strings are value types and do not have an owner flag.
		void SetBorrower() noexcept;
		// Check if this String is an owner of its data (only applicable for long strings; 
        // short strings are value types and do not have an owner flag).
		bool IsOwner() const noexcept;

        // get str length - length in characters (code points)
        uint32_t Length() const noexcept;
        // get str size - length in bytes
        uint32_t Size() const noexcept;
        // get variable size - number of bytes used to store the variable
        uint32_t SizeVar() const noexcept;

        // iterator range support - now returns StringIterator
		// First() returns an iterator to the first UTF-8 code unit of the string.
        StringIterator First() const noexcept;
		// Last() returns an iterator to the first UTF-8 code unit of the last character in the string 
        // (or an invalid iterator if the string is empty or malformed).
        StringIterator Last() const noexcept;
		// Next() returns an iterator to the first UTF-8 code unit of the next character after the current iterator
        StringIterator Next(const StringIterator& current) const noexcept;
		// Previous() returns an iterator to the first UTF-8 code unit of the previous character before the current iterator
        StringIterator Previous(const StringIterator& current) const noexcept;

    private:
        // Layout (private)
        struct { uint8_t tag; uint8_t codeUnits[15]; } shortStr;

    #if INTPTR_MAX == INT64_MAX
        struct { uint8_t tag; uint8_t reserved[3]; uint32_t strSize; const uint8_t* ptr; } longStr;
    #elif INTPTR_MAX == INT32_MAX
        struct { uint8_t tag; uint8_t reserved[3]; uint32_t strSize; uint32_t reserved2; const uint8_t* ptr; } longStr;
    #else
    #error Unsupported pointer size
    #endif
        // copy or zero the union
        struct { uint64_t firstHalf; uint64_t secondHalf; } packedValues;

        // Masks (private) - UPPER_CASE
        static constexpr uint8_t SHORT_MASK  = 0x80;
		static constexpr uint8_t SHORT_FLAG = 0x00; // Short strings have the LONG_FLAG bit cleared (SHORT_FLAG).
        static constexpr uint8_t LONG_MASK   = 0x80;
		static constexpr uint8_t LONG_FLAG = 0x80;
        static constexpr uint8_t OWNER_MASK = 0x40;
        static constexpr uint8_t OWNER_FLAG = 0x40;
        static constexpr uint8_t LONG_OWNER_MASK = (LONG_MASK | OWNER_MASK);
		static constexpr uint8_t LONG_OWNER_FLAG = (LONG_FLAG | OWNER_FLAG);
        static constexpr uint8_t LENGTH_MASK = 0x0F;

        // Helpers (private) - lowerCamelCase
        bool isShort() const noexcept;
        bool isLong()  const noexcept;
		bool isLongOwner() const noexcept;

        void release() noexcept;
        const uint8_t* stringData() const;
    };
#pragma pack(pop)

} // namespace Type
} // namespace Puma

#endif // PUMA_TYPE_STRING_HPP