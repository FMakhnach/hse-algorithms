#include <algorithm>
#include <cmath>
#include <string>
#include <vector>

#include "GrahamScanner.h"
#include "Stack.h"
#include "Point.h"

class GrahamScanner {
public:
	enum class OutputFormat {
		Plain, WKT
	};
	enum class Direction {
		Counterclockwise, Clockwise
	};

	~GrahamScanner() {
		delete convex_hull_;
	}

	GrahamScanner& CalculateConvexHull(std::vector<Point> points) {
		convex_hull_ = new Stack<Point>(points.size());
		if (points.empty()) {
			return *this;
		}
		// Saving the input.
		initial_input_ = points;

		Point start = FindStartingPoint(points);
		convex_hull_->Push(start);
		if (points.size() == 1) {
			return *this;
		}

		// Sorting the points by polar angle.
		std::sort(points.begin(), points.end(),
			[start](const Point& x, const Point& y) {
				return CompareByPolarThenDistance(x, y, start);
			});

		// The actual Graham algorithm.
		convex_hull_->Push(points[1]);
		for (size_t i = 2; i < points.size(); ++i) {
			PopWhileRightTurn(points[i]);
			convex_hull_->Push(points[i]);
		}
		// Connecting with the first point.
		PopWhileRightTurn(points[0]);

		return *this;
	}

	std::string GetData(GrahamScanner::Direction direction, OutputFormat format) {
		std::vector<Point> hull_vector = convex_hull_->ToVector();
		if (direction == Direction::Counterclockwise) {
			std::reverse(hull_vector.begin(), hull_vector.end());
		} else {
			// In this case we have right order initially,
			// but the start element is in the end.
			std::rotate(hull_vector.rbegin(), hull_vector.rbegin() + 1, hull_vector.rend());
		}

		if (format == OutputFormat::Plain) {
			return GetPlainFormat(hull_vector);
		} else if (format == OutputFormat::WKT) {
			return GetWKTFormat(hull_vector);
		}

		return "";
	}

private:
	Stack<Point>* convex_hull_{};
	std::vector<Point> initial_input_;

	static bool CompareByPolarThenDistance(const Point& point1, const Point& point2, const Point& start) {
		double p1_cos = -CalculatePolarCosine(start, point1);
		double p2_cos = -CalculatePolarCosine(start, point2);
		if (std::abs(p1_cos - p2_cos) < 0.00001) {
			return SquareDistance(start, point1) < SquareDistance(start, point2);
		} else {
			return p1_cos < p2_cos;
		}
	}

	static double CalculatePolarCosine(const Point& start, const Point& point) {
		if (point.x == start.x) {
			return point.y == start.y ? 2 : 0;
		}

		double hyp = std::sqrt(SquareDistance(start, point));
		double cat = static_cast<double>(point.x) - start.x;
		return cat / hyp;
	}

	Point FindStartingPoint(const std::vector<Point>& points) {
		Point start = points[0];
		for (const auto p : points) {
			if (p.y < start.y ||
				(p.y == start.y && p.x < start.x)) {
				start = p;
			}
		}
		return start;
	}

	std::string GetPlainFormat(const std::vector<Point>& hull) {
		std::string result = std::to_string(hull.size()) + '\n';
		if (hull.empty()) {
			return result;
		}
		for (size_t i = 0; i < (hull.size() - 1); ++i) {
			result += hull[i].toString();
			result += '\n';
		}
		result += hull.back().toString();
		return result;
	}

	std::string GetWKTFormat(const std::vector<Point>& hull) {
		std::string result = "MULTIPOINT((";
		if (!initial_input_.empty()) {
			for (size_t i = 0; i < (initial_input_.size() - 1); ++i) {
				result += initial_input_[i].toString();
				result += "), (";
			}
			result += initial_input_.back().toString();
		}
		result += "))\nPOLYGON ((";
		if (!hull.empty()) {
			for (const auto& p : hull) {
				result += p.toString();
				result += ", ";
			}
			result += hull[0].toString();
		}
		result += "))";

		return result;
	}

	bool IsRightTurn(const Point& point) {
		return ((convex_hull_->Top().x - convex_hull_->NextToTop().x) *
			(point.y - convex_hull_->Top().y) -
			(convex_hull_->Top().y - convex_hull_->NextToTop().y) *
			(point.x - convex_hull_->Top().x)) <= 0;
	}

	void PopWhileRightTurn(const Point& point) {
		while (convex_hull_->Size() > 1 && IsRightTurn(point)) {
			convex_hull_->Pop();
		}
	}
};