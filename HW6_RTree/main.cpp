#include <algorithm>
#include <boost/geometry/geometry.hpp>
#include <iostream>
#include <fstream>
#include <gdal.h>
#include <ogrsf_frmts.h>
#include <string>

using Point = boost::geometry::model::point<double, 2, boost::geometry::cs::cartesian>;
using Rectangle = boost::geometry::model::box<Point>;
using Node = std::pair<Rectangle, int>;
using RTree = boost::geometry::index::rtree<Node, boost::geometry::index::quadratic<8, 4>>;


Rectangle ToRectangle(const OGREnvelope& envelope) {
	return Rectangle({ envelope.MinX, envelope.MinY }, { envelope.MaxX, envelope.MaxY });
}

// Fills RTree with MBRs of polygons from dataset.
void FillRTree(GDALDataset* dataset, RTree& rtree) {
	OGREnvelope envelope;
	for (auto&& layer : dataset->GetLayers()) {
		for (auto&& feature : layer) {
			feature->GetGeometryRef()->getEnvelope(&envelope);
			Rectangle mbr = ToRectangle(envelope);
			int osm_id = feature->GetFieldAsInteger("OSM_ID");

			rtree.insert(std::make_pair(mbr, osm_id));
		}
	}
}

// Returns ids of all objects in rtree which are intersected by requested_rectangle.
std::vector<int> GetAllIntersectionsIds(const RTree& rtree, const Rectangle& requested_rectangle) {
	std::vector<Node> intersection_nodes;
	rtree.query(boost::geometry::index::intersects(requested_rectangle), std::back_inserter(intersection_nodes));
	std::vector<int> intersection_ids;
	for (auto& node : intersection_nodes) {
		intersection_ids.push_back(node.second);
	}
	return intersection_ids;
}

// Reads a rectangle from file in given format: <minX> <minY> <maxX> <maxY>.
Rectangle ReadRectangleFromFile(const std::string& path) {
	double min_x, min_y, max_x, max_y;
	std::ifstream in(path);
	in >> min_x >> min_y >> max_x >> max_y;
	in.close();
	return Rectangle({ min_x, min_y }, { max_x, max_y });
}

bool TryReadDatasetFromFile(const std::string& file_path, GDALDataset** dataset) {
	*dataset = static_cast<GDALDataset*>(
		GDALOpenEx(file_path.c_str(), GDAL_OF_VECTOR,
			nullptr, nullptr, nullptr));
	return *dataset != nullptr;
}

void WriteToFile(const std::string& path, const std::vector<int>& vector) {
	std::ofstream out(path);
	for (int value : vector) {
		out << value << '\n';
	}
	out.close();
}

int main(int argc, char** argv) {
	if (argc != 4) {
		std::cerr << "You must specify path to data, path to input file and path to output file in command line arguments!";
		return -1;
	}
	std::string data_path = argv[1], input_file_path = argv[2], output_file_path = argv[3];
	std::string shape_file_path = data_path + "/building-polygon.shp";

	GDALAllRegister();
	//Reading the dataset from file.
	GDALDataset* dataset = nullptr;
	if (!TryReadDatasetFromFile(shape_file_path, &dataset)) {
		std::cerr << "Cannot open file!" << std::endl;
		return -1;
	}
	//Filling the RTree with dataset objects.
	RTree rtree;
	FillRTree(dataset, rtree);
	// Reading the rectangle.
	Rectangle requested_rectangle = ReadRectangleFromFile(input_file_path);
	// Constructing result
	std::vector<int> insersections = GetAllIntersectionsIds(rtree, requested_rectangle);
	std::sort(insersections.begin(), insersections.end());

	WriteToFile(output_file_path, insersections);
	return 0;
}
