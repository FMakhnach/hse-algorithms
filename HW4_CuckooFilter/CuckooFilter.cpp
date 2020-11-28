#include <iostream>
#include <fstream>
#include <functional>
#include <map>
#include <string>
#include <random>

// Махнач Ф. БПИ-196

/// <summary>
/// Alias for char. Smallest bit should be 1 for a valid fingerprint.
/// </summary>
typedef char fingerprint_t;

// Object for randoming.
std::mt19937 rrand;

// Returns the next power of 2 of given value (e.g. 5 -> 8).
size_t NextPowerOf2(size_t n) {
	n--;
	n |= n >> 1;
	n |= n >> 2;
	n |= n >> 4;
	n |= n >> 8;
	n |= n >> 16;
	n++;
	return n;
}

// Keeps powers of p = 31 to use in hash.
class Powers {
public:
	static constexpr int MAX_ELEM_LEN{ 15 };
	uint32_t powers[MAX_ELEM_LEN];

	Powers() {
		const uint32_t p = 31;
		powers[0] = 1;
		for (int i = 1; i < MAX_ELEM_LEN; ++i) {
			powers[i] = powers[i - 1] * p;
		}
	}
};

// Holds cuckoo filter information (buckets).
class CuckooHolder {
public:
	// Size of one bucket in fingerprints.
	static constexpr size_t BUCKET_SIZE{ 4 };

	explicit CuckooHolder(size_t num_of_buckets) : num_of_buckets_(num_of_buckets) {
		data_ = new fingerprint_t[BUCKET_SIZE * num_of_buckets]{};
	}

	~CuckooHolder() {
		delete data_;
	}

	// Checks whether there is a given fingerprint in the bucket.
	bool CheckInBucket(size_t bucket_id, fingerprint_t f) const {
		for (size_t i = BUCKET_SIZE * bucket_id; i < BUCKET_SIZE * (bucket_id + 1); ++i) {
			if (data_[i] == f) {
				return true;
			}
		}
		return false;
	}

	size_t GetNumOfBuckets() const {
		return num_of_buckets_;
	}

	// Swaps a given fingerprint with a random fingerprint from the bucket.
	void SwapWithRandomFromBucket(size_t bucket_id, fingerprint_t& f) {
		size_t rnd = rrand() % BUCKET_SIZE;
		fingerprint_t tmp = data_[BUCKET_SIZE * bucket_id + rnd];
		data_[BUCKET_SIZE * bucket_id + rnd] = f;
		f = tmp;
	}

	// Adds f to the bucket if there is empty entry.
	bool TryAdd(size_t bucket_id, fingerprint_t f) {
		int empty_bucket_entry_id = -1;
		for (int i = BUCKET_SIZE - 1; i >= 0; --i) {
			if (data_[BUCKET_SIZE * bucket_id + i] == f) {
				// Already in the bucket, no need to add.
				return true;
			}
			if (data_[BUCKET_SIZE * bucket_id + i] == 0) {
				empty_bucket_entry_id = i;
			}
		}
		// If we've found empty entry we place the fingerprint in it.
		if (empty_bucket_entry_id != -1) {
			data_[BUCKET_SIZE * bucket_id + empty_bucket_entry_id] = f;
			return true;
		}
		return false;
	}

private:
	fingerprint_t* data_;
	size_t num_of_buckets_;
};

class CuckooFilter
{
public:
	static constexpr double FAILURE_PROB{ 0.06 };
	static constexpr int MAX_KICKS{ 500 };

	CuckooFilter(size_t num_of_elements) {
		size_t size = static_cast<size_t>((1 + FAILURE_PROB) * num_of_elements / 4) + 1;
		size = NextPowerOf2(size);
		holder_ = new CuckooHolder(size);
	}

	~CuckooFilter() {
		//delete[] precomputed_powers_;
		delete holder_;
	}

	// Inserts string element to the filter.
	bool Insert(const std::string& elem) {
		fingerprint_t f = Fingerprint(elem);
		size_t i1 = Hash(elem.c_str(), elem.size());
		size_t i2 = (i1 ^ Hash(&f, 1)) % holder_->GetNumOfBuckets();

		if (holder_->TryAdd(i1, f) || holder_->TryAdd(i1, f)) {
			return true;
		}

		size_t i = rrand() % 2 ? i1 : i2;
		for (int n = 0; n < MAX_KICKS; ++n) {
			holder_->SwapWithRandomFromBucket(i, f);
			i = (i ^ Hash(&f, 1)) % holder_->GetNumOfBuckets();
			if (holder_->TryAdd(i, f)) {
				return true;
			}
		}
		return false;
	}

	// Checks whether the given element is in the filter. Can give a false positive (rarely).
	bool Lookup(const std::string& elem) {
		fingerprint_t f = Fingerprint(elem);
		size_t i1 = Hash(elem.c_str(), elem.size());
		size_t i2 = (i1 ^ Hash(&f, 1)) % holder_->GetNumOfBuckets();

		if (holder_->CheckInBucket(i1, f) || holder_->CheckInBucket(i2, f)) {
			return true;
		}
		return false;
	}

private:
	CuckooHolder* holder_;
	static Powers powers;

	// Returns a fingerprint (7-bit hash) of string value. Lowest bit is always 1.
	fingerprint_t Fingerprint(const std::string& val) {
		// 1 is used to indicate non-empty fingerprint (in case hash returns 0 particularly).
		return static_cast<fingerprint_t>(std::hash<std::string>{}(val)) | 1;
	}

	// Polynomial hash
	size_t Hash(const char* bytes, size_t size) {
		size_t hash = 0;
		for (size_t i = 0; i < size; ++i) {
			hash += powers.powers[i] * (static_cast<size_t>(bytes[i] - 'a') + 1);
		}
		return hash % holder_->GetNumOfBuckets();
	}
};

Powers CuckooFilter::powers;

/// <summary>
/// Splits a line into three pieces: command, user and video.
/// </summary>
void ParseLine(const std::string& line, std::string& command, std::string& user, std::string& video) {
	size_t first_space = line.find(' ');
	size_t second_space = first_space + 1 + line.substr(first_space + 1).find(' ');
	command = line.substr(0, first_space);
	user = line.substr(first_space + 1, second_space - first_space - 1);
	video = line.substr(second_space + 1, line.substr(second_space + 1).find(' '));
}

/// <summary>
/// Main program logic.
/// </summary>
void Run(const char* ipath, const char* opath) {
	// Opening the files.
	std::ifstream input;
	input.open(ipath);
	if (!input.is_open()) {
		throw std::runtime_error("Cannot open file " + std::string(ipath));
	}
	std::ofstream output;
	output.open(opath);
	if (!output.is_open()) {
		input.close();
		throw std::runtime_error("Cannot open file " + std::string(opath));
	}

	// Reading the first line.
	std::string line;
	std::getline(input, line);
	if (line.substr(0, line.find(' ')) != "videos") {
		throw std::runtime_error("Unexpected format: no \"videos\" found in the first line.");
	}
	int num_of_videos = stoi(line.substr(line.find(' ') + 1));
	output << "Ok\n";

	// Main part: reading each line and processing the queries 'watch' and 'check'.
	std::map<const std::string, std::unique_ptr<CuckooFilter>> video_history;
	std::string command, user, video;
	while (std::getline(input, line)) {
		ParseLine(line, command, user, video);
		if (command == "watch") {
			// Adding a new user if we don't find one.
			if (video_history.find(user) == video_history.end()) {
				video_history[user] =
					std::unique_ptr<CuckooFilter>(new CuckooFilter(num_of_videos));
			}
			if (video_history[user]->Insert(video)) {
				output << "Ok\n";
			}
			else {
				output << "Failed to insert\n";
			}
		}
		else if (command == "check") {
			// Checking if that person exists and if so looking up the vid.
			bool probably_cond =
				video_history.find(user) != video_history.end()
				&& video_history[user]->Lookup(video);
			output << (probably_cond ? "Probably\n" : "No\n");
		}
		else {
			output.close();
			input.close();
			throw std::runtime_error("Terminated: unknown command \"" + command + "\"");
		}
	}

	// Closing files.
	output.close();
	input.close();
}

int main(int argc, char* argv[]) {
	if (argc != 3) {
		std::cerr << "You must specify input and output files!" << std::endl;
		return 1;
	}
	try {
		Run(argv[1], argv[2]);
	}
	catch (std::runtime_error& e) {
		std::cerr << e.what();
		return 1;
	}

	return 0;
}
