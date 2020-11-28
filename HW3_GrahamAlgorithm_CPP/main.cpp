#include <cstring>
#include <iostream>
#include <fstream>
#include <stdexcept>
#include <vector>

#include "Point.h"
#include "GrahamScanner.h"


GrahamScanner::Direction ParseDirection(char* arg) {
	if (std::strcmp(arg, "cw") == 0) {
		return GrahamScanner::Direction::Clockwise;
	} else if (std::strcmp(arg, "cc") == 0) {
		return GrahamScanner::Direction::Counterclockwise;
	} else {
		throw std::invalid_argument("Invalid direction.");
	}
}

GrahamScanner::OutputFormat ParseFormat(char* arg) {
	if (std::strcmp(arg, "plain") == 0) {
		return GrahamScanner::OutputFormat::Plain;
	} else if (std::strcmp(arg, "wkt") == 0) {
		return GrahamScanner::OutputFormat::WKT;
	} else {
		throw std::invalid_argument("Invalid format.");
	}
}

std::vector<Point> ReadPointsFromFile(char* path) {
	std::ifstream input;
	input.open(path);
	if (!input.is_open()) {
		throw std::runtime_error("File reading failed!");
	}
	std::vector<Point> result;
	int num_of_points, x, y;
	input >> num_of_points;
	while ((input >> x) && (input >> y)) {
		result.emplace_back(Point(x, y));
	}
	if (result.size() != num_of_points) {
		throw std::runtime_error("Incorrect file format! Number of points doesn't match!");
	}
	input.close();
	return result;
}

int main(int argc, char* argv[]) {
	if (argc != 5) {
		std::cerr << "Wrong input! You must specify direction, output format, input and output files.";
		return -1;
	}
	try {
		// Extracting direction and format.
		GrahamScanner::Direction direction = ParseDirection(argv[1]);
		GrahamScanner::OutputFormat format = ParseFormat(argv[2]);

		// Extracting points from input file.
		std::vector<Point> input = ReadPointsFromFile(argv[3]);

		// Calculating result via GrahamScanner object.
		std::string result = GrahamScanner().CalculateConvexHull(input).GetData(direction, format);

		// Writing result to the output file.
		std::ofstream out;
		out.open(argv[4]);
		if (out.is_open()) {
			out << result << std::endl;
		} else {
			std::cerr << "Failed to write to the file!" << std::endl;
		}
		out.close();

		std::cout << "The program execution finished successfully." << std::endl;
	} catch (std::exception& e) {
		std::cerr << e.what() << std::endl;
		return -1;
	}

	return 0;
}
