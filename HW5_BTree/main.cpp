#include <fstream>
#include <iostream>
#include <stdexcept>
#include <string>
#include <vector>

// Махнач Фёдор, БПИ196

class BTree {
private:
	struct KeyValuePair {
		int key;
		int value;
	};

	// Upper-bound binary search for vector of pairs. 
	static int BinarySearch(const std::vector<KeyValuePair>& source, int key) {
		if (source.empty()) {
			return 0;
		}
		if (source.back().key < key) {
			return source.size();
		}
		int left = -1, right = source.size();
		while (right - left > 1) {
			int middle = (left + right) / 2;
			if (source[middle].key == key) {
				return middle;
			} else if (source[middle].key < key) {
				left = middle;
			} else {
				right = middle;
			}
		}
		return right;
	}

	struct Node {
		Node() = default;

		// Makes a node from right part of given node. [center is actually t - 1]
		Node(Node* node, int center) : is_leaf(node->is_leaf) {
			// Taking second half of the keys.
			payload.insert(payload.end(), node->payload.begin() + (center + 1), node->payload.end());
			if (!node->is_leaf) {
				children.insert(children.end(), node->children.begin() + (center + 1), node->children.end());
			}
		}

		bool HasKeyAt(int index, int key) {
			return 0 <= index && index < payload.size() && payload[index].key == key;
		}

		void InsertChild(int index, Node* child) {
			children.insert(children.begin() + index, child);
		}

		void InsertKeyValue(int index, KeyValuePair pair) {
			payload.insert(payload.begin() + index, pair);
		}

		KeyValuePair GetPredecessor(int index) {
			Node* current = children[index];
			while (!current->is_leaf) {
				current = current->children.back();
			}
			return current->payload.back();
		}

		KeyValuePair GetSuccessor(int index) {
			Node* current = children[index + 1];
			while (!current->is_leaf) {
				current = current->children.front();
			}

			return current->payload.front();
		}

		void TakeFromPrevious(int child_index) {
			Node* child = children[child_index];
			Node* prev_sibling = children[child_index - 1];

			child->InsertKeyValue(0, payload[child_index - 1]);

			if (!child->is_leaf) {
				child->InsertChild(0, prev_sibling->children.back());
				prev_sibling->children.pop_back();
			}

			payload[child_index - 1] = prev_sibling->payload.back();

			prev_sibling->payload.pop_back();
		}

		void TakeFromNext(int child_index) {
			Node* child = children[child_index];
			Node* next = children[child_index + 1];

			child->payload.push_back(payload[child_index]);

			if (!child->is_leaf) {
				child->children.push_back(next->children[0]);
			}

			payload[child_index] = next->payload[0];

			// Remove the first element.
			next->payload.erase(next->payload.begin());
			if (!next->is_leaf) {
				next->children.erase(next->children.begin());
			}
		}

		bool is_leaf = true;
		std::vector<Node*> children;
		std::vector<KeyValuePair> payload;
	};

public:
	struct SearchResponse {
		bool is_found;
		int value;
	};

	explicit BTree(int min_branching_degree) : min_branching_degree_(min_branching_degree), root_(new Node()) {}

	~BTree() {
		delete root_;
	}

	// Inserts the given key-value pair into the tree. If the key already present, returns false and does nothing.
	bool Insert(int key, int value) {
		if (Search(key).is_found) {
			return false;
		}

		Node* root = root_;
		if (root_->payload.size() == (2 * min_branching_degree_ - 1)) {
			Node* new_root = new Node();
			root_ = new_root;
			new_root->is_leaf = false;
			new_root->children.push_back(root);
			SplitChild(new_root, 0);
			InsertNonFull(new_root, key, value);
		} else {
			InsertNonFull(root, key, value);
		}

		return true;
	}

	SearchResponse Search(int key) {
		return Search(root_, key);
	}

	SearchResponse Remove(int key) {
		auto res = Remove(root_, key);
		if (root_->payload.empty()) {
			Node* tmp = root_;
			root_ = root_->is_leaf ? new Node() : root_->children.front();
			delete tmp;
		}
		return res;
	}

private:
	Node* root_;
	int min_branching_degree_;

	// Inserts key-value in the non-full node.
	void InsertNonFull(Node* node, int key, int value) {
		int i = BinarySearch(node->payload, key);
		if (node->is_leaf) {
			node->InsertKeyValue(i, { key, value });
			// DISK WRITE node
		} else {
			// DISK READ node.children[i]
			if (node->children[i]->payload.size() == (2 * min_branching_degree_ - 1)) {
				SplitChild(node, i);
				if (key > node->payload[i].key) {
					++i;
				}
			}
			InsertNonFull(node->children[i], key, value);
		}
	}

	// Searches for the key in the given node.
	SearchResponse Search(Node* node, int key) {
		int key_pos = BinarySearch(node->payload, key);
		// If we found the key.
		if (node->HasKeyAt(key_pos, key)) {
			return SearchResponse{ true, node->payload[key_pos].value };
		} else if (node->is_leaf) {
			return SearchResponse{ false, 0 };
		} else {
			return Search(node->children[key_pos], key);
		}
	}

	// Removes the given key from the node or its descendant.
	SearchResponse Remove(Node* node, int key) {
		int key_pos = BinarySearch(node->payload, key);
		// If we have such a key in this node, we can delete it.
		if (node->HasKeyAt(key_pos, key)) {
			int value = node->payload[key_pos].value;
			if (node->is_leaf) {
				RemoveFromLeaf(node, key_pos);
			} else {
				RemoveFromNonLeaf(node, key_pos);
			}
			return { true, value };
		} else {
			// If it is a leaf, there are no descendants.
			if (node->is_leaf) {
				return { false, 0 };
			}

			// Else we are going to look in descendants.
			bool was_last = key_pos == node->payload.size();

			if (node->children[key_pos]->payload.size() < min_branching_degree_) {
				Fill(node, key_pos);
			}

			if (was_last && key_pos > node->payload.size()) {
				return Remove(node->children[key_pos - 1], key);
			}
			return Remove(node->children[key_pos], key);
		}
	}

	// Removes the given key from the node which happened to be a leaf.
	static void RemoveFromLeaf(Node* node, int index) {
		node->payload.erase(node->payload.begin() + index);
	}

	// Removes the given key from non-leaf node.
	void RemoveFromNonLeaf(Node* node, int index) {
		if (node->children[index]->payload.size() > min_branching_degree_) {
			auto pred = node->GetPredecessor(index);
			node->payload[index] = pred;
			Remove(node->children[index], pred.key);
		} else if (node->children[index + 1]->payload.size() > min_branching_degree_) {
			auto succ = node->GetSuccessor(index);
			node->payload[index] = succ;
			Remove(node->children[index + 1], succ.key);
		} else {
			int key = node->payload[index].key;
			Merge(node, index);
			Remove(node->children[index], key);
		}
	}

	// Splitting the child_id'th child of the node into two parts.
	static void SplitChild(Node* node, int child_id) {
		int center_key_id = node->children[child_id]->payload.size() / 2;
		auto left = node->children[child_id];
		auto right = new Node(left, center_key_id);

		node->InsertChild(child_id + 1, right);
		node->InsertKeyValue(child_id, left->payload[center_key_id]);

		left->payload.resize(center_key_id);
		if (!left->is_leaf) {
			left->children.resize(center_key_id + 1);
		}
	}

	// Merges the [index]'th and [index + 1]'th children of the node.
	static void Merge(Node* node, int index) {
		Node* child = node->children[index];
		Node* sibling = node->children[index + 1];

		child->payload.push_back(node->payload[index]);

		// Adding all sibling's payload to the child.
		child->payload.insert(child->payload.end(), sibling->payload.begin(), sibling->payload.end());

		if (!child->is_leaf) {
			child->children.insert(child->children.end(), sibling->children.begin(), sibling->children.end());
		}

		node->payload.erase(node->payload.begin() + index);
		node->children.erase(node->children.begin() + (index + 1));

		delete sibling;
	}

	void Fill(Node* node, int index) const {
		if (index != 0 && CanTakeFrom(node->children[index - 1])) {
			node->TakeFromPrevious(index);
		} else if (index != node->payload.size() && CanTakeFrom(node->children[index + 1])) {
			node->TakeFromNext(index);
		} else {
			if (index != node->payload.size()) {
				Merge(node, index);
			} else {
				Merge(node, index - 1);
			}
		}
	}

	// Indicates whether we can take payload from the node.
	bool CanTakeFrom(const Node* node) const {
		return node->payload.size() > min_branching_degree_;
	}
};

void Run(std::istream& in, std::ostream& out, int t) {
	BTree tree(t);
	std::string command;
	while (in >> command) {
		int key;
		in >> key;
		if (command == "find") {
			auto search_response = tree.Search(key);
			if (search_response.is_found) {
				out << search_response.value << "\n";
			} else {
				out << "null" << "\n";
			}
		} else if (command == "insert") {
			int value;
			in >> value;
			bool insert_response = tree.Insert(key, value);
			out << (insert_response ? "true" : "false") << "\n";
		} else if (command == "delete") {
			auto delete_response = tree.Remove(key);
			if (delete_response.is_found) {
				out << delete_response.value << "\n";
			} else {
				out << "null" << "\n";
			}
		} else {
			throw std::runtime_error("Unknown command \'" + command + "\'");
		}
	}
}

int main(int argc, char* argv[]) {
	if (argc != 4) {
		std::cerr << "You must provide parameter t, input file path and output file path.";
		return 1;
	}
	int t = std::stoi(argv[1]);
	if(t < 2) {
		std::cerr << "Parameter t must be at least 2.";
		return 1;
	}
	std::ifstream in(argv[2]);
	std::ofstream out(argv[3]);
	if (in.is_open() && out.is_open()) {
		Run(in, out, t);
		in.close();
		out.close();
	} else {
		std::cerr << "Cannot open one of the files provided!";
		return 1;
	}
}
