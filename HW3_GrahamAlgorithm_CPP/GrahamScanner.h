#pragma once
class GrahamScanner {
public:
	enum class OutputFormat {
		Plain, WKT
	};
	enum class Direction {
		Counterclockwise, Clockwise
	};

	~GrahamScanner();

	GrahamScanner& CalculateConvexHull(std::vector<Point> points);
	std::string GetData(Direction direction, OutputFormat format);
};
