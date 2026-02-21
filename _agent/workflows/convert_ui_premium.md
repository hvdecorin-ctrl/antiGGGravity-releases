---
description: Apply antiGGGGravity PREMIUM UI Branding Standards (Header, Footer, Typography) to Single, Dual, or Triple panel WPF windows.
---

### 1. Link the Resource Dictionary
Ensure the `Window.Resources` section in your XAML file includes the `Pre_BrandStyles.xaml` dictionary:

```xml
<Window.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="/antiGGGravity;component/Resources/Pre_BrandStyles.xaml"/>
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Window.Resources>
```

### 2. Standardize the Window Outer Border
Wrap the main contents of the window in a Border with the `PremiumBorderStyle`:

```xml
<Border Style="{StaticResource PremiumBorderStyle}">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="70"/> <!-- PremiumHeaderHeight -->
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/> <!-- Footer -->
        </Grid.RowDefinitions>
        ...
    </Grid>
</Border>
```

### 3. Add the Premium Header
Use a Border with `BrandGradientBrush` for the header (Row 0):

```xml
<Border Grid.Row="0" Background="{StaticResource BrandGradientBrush}" CornerRadius="12,12,0,0">
    <Grid Margin="25,0">
        <StackPanel VerticalAlignment="Center">
            <TextBlock Text="TOOL TITLE" Style="{StaticResource PremiumTitleStyle}"/>
            <TextBlock Text="Tool subtitle or active state" Style="{StaticResource PremiumSubtitleStyle}"/>
        </StackPanel>
        <Button HorizontalAlignment="Right" VerticalAlignment="Center" Content="✕" 
                Style="{StaticResource PremiumPrimaryButtonStyle}" Background="Transparent" BorderThickness="0" Foreground="White" FontSize="16"
                Click="Close_Click" WindowChrome.IsHitTestVisibleInChrome="True"/>
    </Grid>
</Border>
```

### 4. Implement Standard Content Panels
Choose the appropriate layout from the samples:
- **Single Panel**: Use a single content Grid with 25px margin.
- **Dual Panel**: Use two columns separated by a 1px vertical divider.
- **Triple Panel**: Use three columns with dividers.

Always use `PremiumSectionLabelStyle` for headers inside the content.

### 5. Setup the Standard Footer
The footer should contain status info on the left and primary actions on the right:

```xml
<Border Grid.Row="2" Padding="20,15" Background="#FAFAFA" CornerRadius="0,0,12,12" BorderBrush="{StaticResource PremiumBorderBrush}" BorderThickness="0,1,0,0">
    <Grid>
        <TextBlock Text="Status..." VerticalAlignment="Center" Foreground="#999999"/>
        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
            <Button Content="Cancel" Style="{StaticResource PremiumPrimaryButtonStyle}" Margin="0,0,10,0"/>
            <Button Content="Apply" Style="{StaticResource PremiumActionButtonStyle}" Width="100"/>
        </StackPanel>
    </Grid>
</Border>
```

### 6. Reference Samples
For full structural references, see:
- `Views\Samples\PremiumSinglePanel.xaml`
- `Views\Samples\PremiumDualPanel.xaml`
- `Views\Samples\PremiumTriplePanel.xaml`
