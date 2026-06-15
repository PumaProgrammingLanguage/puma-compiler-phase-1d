// PumaType.cpp : Defines the functions for the static library.
//
#include "String.hpp"
#include <memory.h>
#include <new>

namespace Puma {
namespace Type
{

	// Default-constructs an empty String (no heap allocation).
	String::String() noexcept
	{
		// Initialize the union to represent an empty string (short string with length 0).
		packedValues.firstHalf = 0;
		packedValues.secondHalf = 0;
	}

	// Constructs a String from UTF‑8 string literal (null-terminated C-string).
	String::String(const char* utf8) noexcept
		: String(reinterpret_cast<const uint8_t*>(utf8), static_cast<uint32_t>(strlen(utf8)))
	{
	}

	// Constructs a String from raw UTF‑8 bytes and explicit length in bytes.
	String::String(const char* utf8, size_t dataSize) noexcept
		: String(reinterpret_cast<const uint8_t*>(utf8), static_cast<uint32_t>(dataSize))
	{
	}

	// Constructs a String from raw UTF‑8 bytes and explicit length in bytes.
	String::String(const uint8_t* utf8, uint32_t dataSize) noexcept
		: String()
	{
		if (utf8 == nullptr || dataSize == 0)
		{
			// Empty string: no heap allocation needed, just leave the default empty state.
			return;
		}
		// Check if the string can be stored as a short string (fits within the codeUnits array).
		if (dataSize <= sizeof(shortStr.codeUnits))
		{
			// Short string: store directly in the union (no heap allocation).
			// Long flag and owner flags are already 0. 
			
			// Length is stored in the lower 4 bits of the tag.
			shortStr.tag = static_cast<uint8_t>(dataSize & LENGTH_MASK);
			if (dataSize > 0)
			{
				// Copy the string data into the codeUnits array.
				memcpy(shortStr.codeUnits, utf8, dataSize);
			}
		}
		else
		{
			uint8_t* buf = new (nothrow) uint8_t[dataSize];
			if (buf != nullptr)
			{
				// Copy the string data into the newly allocated buffer.
				memcpy(buf, utf8, dataSize);
				longStr.ptr = buf;
				// Set as long string with ownership.
				longStr.tag = LONG_OWNER_FLAG;
				// Store the string size in bytes.
				longStr.strSize = static_cast<uint32_t>(dataSize);
			}
			else
			{
				// Allocation failed: fallback to empty string (no heap allocation).
				packedValues.firstHalf = 0;
				packedValues.secondHalf = 0;
			}
		}
	}

	String::String(String& source, bool moveOwner) noexcept
		: String()
	{
		// Copy the source string's data into this string.
		packedValues.firstHalf = source.packedValues.firstHalf;
		packedValues.secondHalf = source.packedValues.secondHalf;

		// check if we should transfer ownership
		if(source.isLongOwner())
		{
			if (moveOwner)
			{
				// transfer ownership of the string data from the source to this string.
				// Set the source string as a borrower (non-owner) since ownership is being transferred.
				source.SetBorrower();
				// Set this string as the new owner of the data.
				SetOwner();
			}
			else
			{
				// Do not transfer ownership. This string will be a borrower (non-owner) of the same data as the source string.
				SetBorrower();
			}
		}
		// else, default is borrower (non-owner) since string copying typically doesn't transfer ownership.
	}

	String::~String() noexcept
	{
		release();
	}

	String& String::operator=(const String& source) noexcept
	{
		if (this == &source)
		{
			return *this;
		}

		// Release current resources before copying new data.
		release();
		// Copy the source string's data into this string.
		packedValues.firstHalf = source.packedValues.firstHalf;
		packedValues.secondHalf = source.packedValues.secondHalf;
		// String assignments doesn't transfer ownership.
		if (isLongOwner())
		{
			SetBorrower();
		}
		// else, if source is a short string, it is a value type and doesn't have ownership semantics.
		// else, if source is a long string but not an owner, it is a borrower and we also don't transfer ownership.
		return *this;
	}

	// Less-than operator for ordering of a UTF-8 string based on lexicographical order of the UTF-8 bytes.
	bool operator<(const String& source) noexcept
	{
		const uint32_t thisLength = Length();
		const uint32_t sourceLength = source.Length();
		const uint32_t minLength = (thisLength < sourceLength) ? thisLength : sourceLength;
		const StringIterator thisIter = this.First();
		const StringIterator sourceIter = source.First();

		while (thisIter.IsValid() && sourceIter.IsValid() && minLength > 0)
		{
			const uint8_t thisByte = *thisIter;
			const uint8_t sourceByte = *sourceIter;
			if (thisByte < sourceByte)
			{
				return true; // This string is less than the source string.
			}
			else if (thisByte > sourceByte)
			{
				return false; // This string is greater than the source string.
			}
			// bytes are equal. continue comparing the next character.
			thisIter.Next();
			sourceIter.Next();
		}
		// If all bytes are equal up to the length of the shorter string, then the shorter string is considered less.
		return (thisLength < sourceLength);
	}

	// Greater-than operator for ordering
	bool operator>(const String& source) noexcept
	{
		const uint32_t thisLength = Length();
		const uint32_t sourceLength = source.Length();
		const uint32_t minLength = (thisLength < sourceLength) ? thisLength : sourceLength;
		const StringIterator thisIter = this.First();
		const StringIterator sourceIter = source.First();
		while (thisIter.IsValid() && sourceIter.IsValid() && minLength > 0)
		{
			const uint8_t thisByte = *thisIter;
			const uint8_t sourceByte = *sourceIter;
			if (thisByte > sourceByte)
			{
				return true; // This string is greater than the source string.
			}
			else if (thisByte < sourceByte)
			{
				return false; // This string is less than the source string.
			}
			// bytes are equal. continue comparing the next character.
			thisIter.Next();
			sourceIter.Next();
		}
		// If all bytes are equal up to the length of the shorter string, then the longer string is considered greater.
		return (thisLength > sourceLength);
	}


	// Equality
	bool operator==(const String& source) noexcept
	{
		const uint32_t thisLength = Length();
		const uint32_t sourceLength = source.Length();
		if (thisLength != sourceLength)
		{
			return false; // Strings of different lengths cannot be equal.
		}
		const StringIterator thisIter = this.First();
		const StringIterator sourceIter = source.First();
		while (thisIter.IsValid() && sourceIter.IsValid())
		{
			const uint8_t thisByte = *thisIter;
			const uint8_t sourceByte = *sourceIter;
			if (thisByte != sourceByte)
			{
				return false; // Found a byte that differs, so strings are not equal.
			}
			thisIter.Next();
			sourceIter.Next();
		}
		return true; // All bytes are equal and lengths are the same, so strings are equal.
	}

	// Inequality
	bool operator!=(const String& source) noexcept
	{
		return !(*this == source);
	}

	// Less-than-or-equal operator for ordering
	bool operator<=(const String& source) noexcept
	{
		return !(*this > source);
	}

	// Greater-than-or-equal operator for ordering
	bool operator>=(const String& source) noexcept
	{
		return !(*this < source);
	}

	bool String::isShort() const noexcept
	{
		// A short string is identified by the LONG_FLAG bit not set (SHORT_FLAG) in the tag.
		return (shortStr.tag & SHORT_MASK) == SHORT_FLAG;
	}

	bool String::isLong() const noexcept
	{
		// A long string is identified by the LONG_FLAG bit set in the tag.
		return (longStr.tag & LONG_MASK) == LONG_FLAG;
	}

	bool String::isLongOwner() const noexcept
	{
		// A long string is an owner if both the LONG_FLAG and OWNER_FLAG bits are set in the tag.
		return (longStr.tag & LONG_OWNER_MASK) == LONG_OWNER_FLAG;
	}

	void String::SetOwner() noexcept
	{
		// Only applicable for long strings; short strings are value types (stored directly in the union).
		if (isLong())
		{
			// Set the owner flag for long strings.
			longStr.tag |= OWNER_FLAG;
		}
		// Short strings are value types and do not have an owner flag, so we do nothing for short strings.
	}

	void String::SetBorrower() noexcept
	{
		// Only applicable for long strings; short strings are value types (stored directly in the union).
		if (isLong())
		{
			// Clear the owner flag for long strings.
			longStr.tag &= ~OWNER_FLAG;
		}
		// Short strings are value types and do not have an owner flag, so we do nothing for short strings.
	}

	bool String::IsOwner() const noexcept
	{
		// Only applicable for long strings; short strings are value types (stored directly in the union).
		return isLongOwner();
	}

	uint32_t String::Length() const noexcept
	{
		uint32_t charCount = 0;
		const uint8_t* ptr = isShort() ? shortStr.codeUnits : longStr.ptr;
		const uint32_t strSize = Size();

		for (uint32_t i = 0; i < strSize; )
		{
			const uint8_t c = ptr[i];
			const uint8_t charSize = Character::GetCharSize(c);
			i += charSize;
			++charCount;
		}

		return charCount;
	}

	uint32_t String::Size() const noexcept
	{
		return isShort()
			? static_cast<uint32_t>(shortStr.tag & LENGTH_MASK)
			: static_cast<uint32_t>(longStr.strSize);
	}

	uint32_t String::SizeVar() const noexcept
	{
		return sizeof(String);
	}

	// Returns a pointer to the UTF-8 bytes of the string. The caller is responsible for deleting the returned buffer.
	const uint8_t* String::ToUTF8() const noexcept
	{
		uint8_t* buf = new (nothrow) uint8_t[Size() + 1];
		if (buf != nullptr)
		{
			// Copy the string data into the newly allocated buffer.
			memcpy(buf, stringData(), Size());
			// Null-terminate the buffer.
			buf[Size()] = '\0';
			return buf;
		}
		else
		{
			// Allocation failed: return nullptr.
			return nullptr;
		}
	}

	const uint8_t* String::stringData() const
	{
		return (this->isShort() ? this->shortStr.codeUnits : this->longStr.ptr);
	}

	void String::release() noexcept
	{
		if (isLongOwner() && longStr.ptr != nullptr)
		{
			delete[] longStr.ptr;
		}

		packedValues.firstHalf = 0;
		packedValues.secondHalf = 0;
	}

	String String::ToString() noexcept
	{
		// Return a copy of this string (copy constructor semantics).
		return String(*this);
	}

	// Iterator support: first code unit.
	StringIterator String::First() const noexcept
	{
		return StringIterator(*this);
	}

	// Iterator support: iterator to first byte of last UTF‑8 character (or invalid if empty/malformed).
	StringIterator String::Last() const noexcept
	{
	    const uint8_t* first = (const uint8_t*)ToUTF8();
	    const uint32_t size  = Size();

	    if (size == 0 || first == nullptr)
	    {
	        return StringIterator();
	    }

	    const uint8_t* p = first + size;

	    while (p > first)
	    {
	        --p;
	        const uint8_t byte = *p;

	        if ((byte & 0xC0u) != 0x80u)
	        {
	            return StringIterator(p, first);
	        }
	    }

	    // malformed; no valid leading byte found
	    return StringIterator();
	}

	// Iterator support: advance to the next UTF‑8 code point.
	StringIterator String::Next(const StringIterator& current) const noexcept
	{
		if (!current.IsValid())
		{
			return StringIterator();
		}

		// Copy, then advance one UTF‑8 character.
		StringIterator nextIt = current;
		++nextIt;

		return nextIt;
	}

	// Iterator support: move to the previous UTF‑8 code point.
	StringIterator String::Previous(const StringIterator& current) const noexcept
	{
		if (!current.IsValid())
		{
			return StringIterator();
		}

		// Copy and move back one UTF‑8 character using the iterator's operator--().
		StringIterator prevIt = current;
		--prevIt;

		return prevIt;
	}

} // namespace Type
} // namespace Puma