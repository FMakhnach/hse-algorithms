#pragma once
template<typename T>
class Stack {
public:
	Stack();
	explicit Stack(size_t max_size);
	Stack(const Stack<T>& other);
	~Stack();

	bool IsEmpty() const;
	/**
	 * Returns the top element of the stack.
	 * Throws exception if there is no next-to-top element.
	 * @throws std::logic_error
	 */
	T NextToTop() const;
	/**
	 * Removes an element from top of the stack. Throws exception if the stack is empty.
	 * @throws std::logic_error
	 */
	void Pop();
	/**
	 * Adds an element at the top of the stack. Throws exception if the stack is full.
	 * @throws std::logic_error
	 */
	void Push(T item);
	size_t Size() const;
	/**
	 * Returns the top element of the stack. Throws exception if the stack is empty.
	 * @throws std::logic_error
	 */
	T Top() const;
	/**
	 * Converts the stack to an array (from top to the bottom).
	 */
	std::vector<T> ToVector() const;
};

