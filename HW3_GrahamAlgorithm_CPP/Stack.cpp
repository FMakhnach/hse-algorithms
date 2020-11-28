#include <vector>
#include <stdexcept>
#include "Stack.h"

template<typename T>
class Stack {
private:
	struct Node {
		const T value;
		Node* next;
	};
public:
	Stack() : top_(nullptr), size_(0), max_size_(MAXSIZE) {}

	explicit Stack(size_t max_size) : top_(nullptr), size_(0), max_size_(max_size) {
		// I don't like it and I don't understand the point of MaxSize.
		if (max_size > MAXSIZE) {
			throw std::invalid_argument(&"Max size cannot be bigger than "[max_size]);
		}
	}

	Stack(const Stack<T>& other) : top_(nullptr), size_(0), max_size_(other.max_size_) {
		std::vector<T> vec = other.toVector();
		for (size_t i = vec.size() - 1; i < vec.size(); --i) {
			push(vec[i]);
		}
	}

	~Stack() {
		while (top_) {
			pop();
		}
	}

	bool IsEmpty() const {
		return !top_;
	}

	size_t Size() const {
		return size_;
	}

	T NextToTop() const {
		if (size_ < 2) {
			throw std::logic_error("There is no next-to-top element in the stack!");
		}
		return top_->next->value;
	}

	void Push(T item) {
		if (size_ == max_size_) {
			throw std::logic_error("Stack is full, cannot push a new element!");
		}
		top_ = new Node{ item, top_ };
		++size_;
	}

	void Pop() {
		if (size_ == 0) {
			throw std::logic_error("Stack is empty, cannot pop an element!");
		}
		Node* next = top_->next;
		delete top_;
		top_ = next;
		--size_;
	}

	T Top() const {
		if (IsEmpty()) {
			throw std::logic_error("Stack is empty, cannot get top element!");
		}
		return top_->value;
	}

	std::vector<T> ToVector() const {
		std::vector<T> result;
		Node* it = top_;
		while (it != nullptr) {
			result.push_back(it->value);
			it = it->next;
		}
		return result;
	}

private:
	Node* top_;
	size_t max_size_;
	size_t size_;
	static const size_t MAXSIZE = 1000000;
};
