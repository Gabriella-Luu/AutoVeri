import os
import re
import pandas as pd

# 定义文件夹路径
folder_path = '/Users/luyihan/Desktop/AutoVeri/speceval_output'  # 替换为你的文件夹路径

# 初始化一个空的DataFrame来存储结果
# columns = ['Filename', 'Stat1', 'Stat2', 'Stat3', 'Stat4', 'Stat5', 'Stat6', 'Average Correctness', 'Average Completeness']
columns = ['Filename', 'Average Correctness', 'Average Completeness']
df = pd.DataFrame(columns=columns)

# 遍历文件夹中的所有txt文件
for filename in os.listdir(folder_path):
    if filename.endswith('.txt'):
        file_path = os.path.join(folder_path, filename)
        with open(file_path, 'r') as file:
            lines = file.readlines()
            
            # 提取Dafny Statistics数据
            # stats_lines = [line for line in lines if 'Dafny Statistics for test:' in line]
            # stats_data = []
            # for line in stats_lines:
            #     # 使用正则表达式提取方括号中的数字
            #     match = re.search(r'\[([\d, ]+)\]', line)
            #     if match:
            #         stats = match.group(1).split(', ')
            #         stats_data.append(stats)

            
            # 提取Average Correctness和Average Completeness数据
            avg_lines = [line for line in lines if 'Average Correctness:' in line]
            if avg_lines:  # 检查是否有匹配的行
                avg_line = avg_lines[0]
                avg_correctness = re.search(r'Average Correctness: ([\d.]+)', avg_line).group(1)
                avg_completeness = re.search(r'Average Completeness: ([\d.]+)', avg_line).group(1)
                row = [filename] + [avg_correctness, avg_completeness]
                df.loc[len(df)] = row
            else:
                row = [filename] + ["0.0", "0.0"]
                df.loc[len(df)] = row
            

# 保存DataFrame到Excel文件
output_excel_path = '/Users/luyihan/Desktop/AutoVeri/speceval_output/output.xlsx'  # 替换为你想保存的Excel文件路径
df.to_excel(output_excel_path, index=False)

print(f"数据已成功保存到 {output_excel_path}")